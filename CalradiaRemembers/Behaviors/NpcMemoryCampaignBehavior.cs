// Code written by Gabriel Mailhot, 19/06/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Commoners;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Dialogue;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Behaviors
{
   /// <summary>
   ///   Bridges the NpcMemoryService SDK to the Bannerlord campaign lifecycle.
   ///   Phase 7.2 adds:
   ///   - <see cref="BannerlordNpcMemoryStore" /> wired to <see cref="SyncData" />.
   ///   - A test NPC profile (Raganvad) created on first run to validate persistence.
   ///   Sprint 7.3.A adds:
   ///   - <see cref="LlmServices" /> initialized at construction.
   ///   - 'campaign.test_llm' cheat command (see <see cref="ModCheats" />).
   ///   Sprint 7.3.B adds:
   ///   - <see cref="GameStateBridge" /> for the encounter-context pipeline.
   ///   - Public accessor for <see cref="Store" /> so the diagnostic can persist mapped profiles.
   ///   Sprint 7.3.C1 adds:
   ///   - <see cref="LlmDialogFlow" /> registration on session launch.
   ///   Sprint 7.6.F adds:
   ///   - <see cref="VanillaDialogueRecorder" /> subscription on session launch.
   /// </summary>
   public sealed class NpcMemoryCampaignBehavior : CampaignBehaviorBase
   {
      public static NpcMemoryCampaignBehavior Instance;

      // ONE LlmDialogFlow, owned by the behavior. Accessible from outside via Instance.
      public readonly LlmDialogFlow DialogFlow = new();

      // ONE VanillaDialogueRecorder, owned by the behavior. Holds a real reference
      // so the instance survives as long as the behavior does.
      public readonly VanillaDialogueRecorder DialogueRecorder = new();
      public readonly IGameStateBridge GameStateBridge = new BannerlordGameStateBridge();

      // ONE LetterTriggerEvaluator, owned by the behavior. Handles tournament/battle
      // event hooks and daily romantic/spouse correspondence checks.
      public readonly LetterTriggerEvaluator LetterEvaluator = new();
      public readonly LlmServices LlmServices = new();

      // ONE QuestDeedTracker, owned by the behavior.
      public readonly QuestDeedTracker QuestTracker = new();
      public readonly BannerlordNpcMemoryStore Store = new();

      // ONE WorldEventFeeders, owned by the behavior. Records dated world happenings
      // (battles, sieges, marriages, executions, wars) into the WorldEventStore (v1.2).
      public readonly WorldEventFeeders WorldFeeders = new();

      public NpcMemoryCampaignBehavior()
      {
         Instance = this;
         LlmServices.Initialize();
      }


      // ── Lifecycle hooks ───────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
            this, OnSessionLaunched);
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(
            this, OnDailyTick);
         // Sprint 12c: detect when a player-letter courier is destroyed in transit.
         CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(
            this, OnMobilePartyDestroyed);
         // Sprint 12c: deliver promptly when a courier enters the recipient's settlement.
         CampaignEvents.SettlementEntered.AddNonSerializedListener(
            this, OnSettlementEntered);
         // Sprint 12c: deliver within 1 game hour of the courier catching the recipient,
         // instead of waiting up to 1 game day for the daily tick.
         CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(
            this, OnHourlyTickParty);
         // Save safety: keep courier parties out of the .sav (a mod party in an odd state
         // can corrupt the native load). Despawn before the save, respawn once it is over.
         CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(
            this, OnBeforeSave);
         CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(
            this, OnSaveOver);
         // Close a giver's open quests the instant they are slain, instead of leaving them
         // stuck in the journal until the next reload (the daily pass is the safety net).
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(
            this, OnHeroKilled);
      }

      /// <summary>
      ///   When any hero dies, close the quests they had given the player: an outstanding task, or one
      ///   already done whose reward can no longer be collected. Not the player's fault, so no penalty.
      /// </summary>
      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         SafeRun.Step("OnHeroKilled.CancelQuestsOfDeadGiver", () =>
         {
            if (victim == null) return;

            NpcProfile giver = Store?.Get(victim.StringId);
            if (giver == null || giver.ActiveQuests.Count == 0) return;

            QuestService.CancelQuestsOfDeadGiver(giver, victim, GameStateBridge.GetCurrentGameDay(), Store);
         });
      }

      /// <summary>
      ///   Called by Bannerlord on every save and load.
      ///   We delegate entirely to the store — it handles both directions.
      /// </summary>
      public override void SyncData(IDataStore dataStore)
         => Store.SyncData(dataStore);

      #region private

      /// <summary>
      ///   Shows the contents of all stored profiles in the in-game console.
      ///   On first run: shows the freshly created test profile.
      ///   After save/load: shows the profile reloaded from the save file.
      /// </summary>
      private void DisplayLoadedProfiles()
      {
         IReadOnlyList<string> ids = Store.ListIds();
         if (!ids.Any())
         {
            ModLog.Info("Store is empty.");

            return;
         }

         foreach (string id in ids)
         {
            NpcProfile p = Store.Get(id);

            if (p == null) continue;

            ModLog.Info($"  {p.Name} | {p.Events.Count} event(s) | rep:{p.ReputationWithPlayer}");
            foreach (NotableEvent ev in p.Events)
               ModLog.Info($"    [{ev.type}] Day {ev.gameDay}: {ev.summary}");
         }
      }

      /// <summary>Removes all courier parties just before the world is serialized.</summary>
      private void OnBeforeSave()
         => CourierService.DespawnAll(Store);

      /// <summary>
      ///   Runs the quest lifecycle pass once per game day: expire overdue quests
      ///   (with a consequence) and cancel ones whose war premise has ended.
      /// </summary>
      private void OnDailyTick()
      {
         int today = GameStateBridge.GetCurrentGameDay();
         // Each subsystem runs behind its own guard: one throwing must never starve the rest of
         // the day's maintenance (letters, quests, couriers, captive triggers are independent).
         SafeRun.Step("OnDailyTick.QuestService.RunDailyMaintenance", () => QuestService.RunDailyMaintenance(Store, today));
         SafeRun.Step("OnDailyTick.LetterDeliveryService.CheckDeliveries", () => LetterDeliveryService.CheckDeliveries(Store, today));
         SafeRun.Step("OnDailyTick.PlayerLetterService.CheckDeliveries", () => PlayerLetterService.CheckDeliveries(Store, today));
         SafeRun.Step("OnDailyTick.LetterEvaluator.EvaluateDaily", () => LetterEvaluator.EvaluateDaily(Store, LlmServices, today));
         SafeRun.Step("OnDailyTick.TriggerPendingReplies", () => TriggerPendingReplies(today));
         SafeRun.Step("OnDailyTick.SpawnPendingReplyCouriers", () => SpawnPendingReplyCouriers(today));
         SafeRun.Step("OnDailyTick.CaptiveEncounterService.CheckAndTrigger", () => CaptiveEncounterService.CheckAndTrigger(Store, LlmServices));
      }

      /// <summary>
      ///   Fires every game hour for each party. Used to deliver courier letters within
      ///   1 hour of arrival rather than waiting for the daily tick.
      /// </summary>
      private void OnHourlyTickParty(MobileParty party)
      {
         CourierService.CheckCourierArrival(party, Store);
      }

      /// <summary>
      ///   When any mobile party is destroyed, check whether it was a letter courier
      ///   and, if so, mark the carried letter lost (handled in CourierService).
      /// </summary>
      private void OnMobilePartyDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         CourierService.HandlePartyDestroyed(destroyed, Store);
      }

      /// <summary>Restores the in-flight couriers the pre-save despawn removed.</summary>
      private void OnSaveOver(bool isSuccessful, string saveName)
         => CourierService.RespawnInFlight(Store);


      // ── Session launched ──────────────────────────────────────────────────

      private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
      {
         ModLog.Info("Campaign session launched.");
         // Each start-up step runs behind its own guard: a single loader/subscription throwing
         // must never prevent the rest of the session from wiring itself up.
         SafeRun.Step("OnSessionLaunched.CulturalTraditionsLoader.Load", CulturalTraditionsLoader.Load);
         SafeRun.Step("OnSessionLaunched.NarrativeLoader.Load", NarrativeLoader.Load);
         SafeRun.Step("OnSessionLaunched.JealousyCultureLoader.Load", JealousyCultureLoader.Load);
         SafeRun.Step("OnSessionLaunched.LlmServices.Invalidate", LlmServices.Invalidate); // rebuild PromptBuilder with fresh file content
         SafeRun.Step("OnSessionLaunched.StoryLog.WriteSessionHeader", StoryLog.WriteSessionHeader);
         SafeRun.Step("OnSessionLaunched.CaptiveEncounterService.ResetOnSessionLaunch", CaptiveEncounterService.ResetOnSessionLaunch);
         SafeRun.Step("OnSessionLaunched.DisplayLoadedProfiles", DisplayLoadedProfiles);
         SafeRun.Step("OnSessionLaunched.UpgradeLegacyProfiles", UpgradeLegacyProfiles);
         SafeRun.Step("OnSessionLaunched.RefreshRelationships", RefreshRelationships);
         SafeRun.Step("OnSessionLaunched.RumorStore.Clear", RumorStore.Clear);
         SafeRun.Step("OnSessionLaunched.DialogFlow.Register", () => DialogFlow.Register(campaignGameStarter));
         ModLog.Info("Dialog flow registered.");

         SafeRun.Step("OnSessionLaunched.DialogueRecorder.Subscribe", DialogueRecorder.Subscribe);
         SafeRun.Step("OnSessionLaunched.QuestTracker.Subscribe", QuestTracker.Subscribe);
         SafeRun.Step("OnSessionLaunched.LetterEvaluator.Subscribe", LetterEvaluator.Subscribe);
         SafeRun.Step("OnSessionLaunched.WorldFeeders.Subscribe", WorldFeeders.Subscribe);
         SafeRun.Step("OnSessionLaunched.TaverneLetterBehavior.Register", () => new TaverneLetterBehavior().Register(campaignGameStarter));
         SafeRun.Step("OnSessionLaunched.CommonsDialogBehavior.Register", () => new CommonsDialogBehavior().Register(campaignGameStarter));
         SafeRun.Step("OnSessionLaunched.CourierService.RebuildActiveCourierSet", () => CourierService.RebuildActiveCourierSet(Store));
         // Couriers were kept out of the save (despawned pre-save). Don't respawn a fragile
         // party during session launch: instead hand any orphaned in-flight letter back to
         // its date-based delivery, so it still arrives even though the courier is gone.
         SafeRun.Step("OnSessionLaunched.CourierService.ClearOrphanedCouriers", () => CourierService.ClearOrphanedCouriers(Store));
         SafeRun.Step("OnSessionLaunched.RemoveOrphanedLetterPlaceholders", RemoveOrphanedLetterPlaceholders);
         SafeRun.Step("OnSessionLaunched.QuestJournalBridge.ReconcileOnSessionLaunch", () => QuestJournalBridge.ReconcileOnSessionLaunch(Store));
         SafeRun.Step("OnSessionLaunched.CompanionErrandJournalBridge.ReconcileOnSessionLaunch", CompanionErrandJournalBridge.ReconcileOnSessionLaunch);
      }

      /// <summary>
      ///   When a party enters a settlement, check whether it is a letter courier
      ///   reaching its recipient and, if so, deliver immediately.
      /// </summary>
      private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
      {
         CourierService.HandleSettlementEntered(party, settlement, Store);
      }

      private void RefreshRelationships()
      {
         var refreshed = 0;
         foreach (string id in Store.ListIds())
         {
            NpcProfile profile = Store.Get(id);

            if (profile == null) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero == null) continue;

            HeroProfileMapper.RefreshPersonality(profile, hero);
            profile.Relationships = RelationshipsBuilder.Build(hero);
            refreshed++;
         }

         if (refreshed > 0)
            ModLog.Info($"Refreshed personality and relationships for {refreshed} profile(s).");
      }

      /// <summary>
      ///   Removes letter placeholders left behind when an LLM generation never finished in a
      ///   PRIOR session (<c>Content</c> still null, <c>DispatchDay</c> still the sentinel —
      ///   the async task died with that session). Runs only at session launch: at that point
      ///   no generation is in flight, so an orphan-shaped placeholder is always genuinely stale.
      ///   Running this on the daily tick instead would delete the placeholder of a letter being
      ///   generated on that very tick (the generation adds the placeholder synchronously, then
      ///   awaits the LLM), stranding the letter and re-triggering its sender every day.
      /// </summary>
      private void RemoveOrphanedLetterPlaceholders()
      {
         foreach (string id in Store.ListIds())
         {
            NpcProfile profile = Store.Get(id);

            if (profile?.SentLetters == null) continue;

            var orphans = new List<PendingLetter>();
            foreach (PendingLetter letter in profile.SentLetters)
            {
               if (!string.IsNullOrWhiteSpace(letter.Content) || letter.DispatchDay != int.MaxValue || letter.IsDelivered) continue;

               orphans.Add(letter);
               ModLog.Info($"[LETTER] removing orphaned placeholder from {profile.Name} (LLM never responded last session).");
            }

            if (orphans.Count == 0) continue;
            foreach (PendingLetter orphan in orphans)
               profile.SentLetters.Remove(orphan);
            Store.Set(profile);
         }
      }

      /// <summary>
      ///   Spawns reply couriers (NPC→player) whose <c>DispatchDay</c> has arrived.
      ///   Must run on the main thread — MobileParty creation is not thread-safe.
      /// </summary>
      private void SpawnPendingReplyCouriers(int today)
      {
         foreach (string id in Store.ListIds())
         {
            NpcProfile profile = Store.Get(id);

            if (profile?.SentLetters == null) continue;

            var changed = false;

            foreach (PendingLetter letter in profile.SentLetters)
            {
               if (letter.IsDelivered || letter.IsLost) continue;
               if (!string.IsNullOrEmpty(letter.CourierPartyId)) continue; // already dispatched
               if (letter.DispatchDay == 0 || today < letter.DispatchDay) continue;

               Hero sender = Hero.FindFirst(h => h.StringId == profile.Id);

               if (sender == null) continue;

               if (CourierService.TrySpawnReplyCourier(letter, sender))
                  changed = true;
            }

            if (changed) Store.Set(profile);
         }
      }

      /// <summary>
      ///   Scans for delivered player letters that have not yet triggered an LLM reply
      ///   and queues the reply generation (async). Sets <c>HasTriggeredReply</c> to
      ///   prevent duplicate calls on subsequent ticks.
      /// </summary>
      private void TriggerPendingReplies(int today)
      {
         foreach (string id in Store.ListIds())
         {
            NpcProfile profile = Store.Get(id);

            if (profile?.ReceivedPlayerLetters == null) continue;

            var changed = false;
            foreach (PlayerLetter letter in profile.ReceivedPlayerLetters)
            {
               if (!letter.IsDelivered || letter.IsLost || letter.HasTriggeredReply) continue;
               letter.HasTriggeredReply = true;
               changed = true;

               Hero hero = Hero.FindFirst(h => h.StringId == profile.Id);

               if (hero == null) continue;

               LetterGenerationService.QueueReplyToPlayerLetter(
                  profile, hero, letter, LlmServices, Store, today);
            }

            if (changed) Store.Set(profile);
         }
      }

      private void UpgradeLegacyProfiles()
      {
         var upgraded = 0;
         foreach (string id in Store.ListIds())
         {
            NpcProfile profile = Store.Get(id);

            if (profile == null) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == id);

            if (hero == null) continue; // hero gone — skip silently

            bool beforeNeedsRomantic = profile.Romantic == null;
            bool beforeStaleTrait = string.IsNullOrEmpty(profile.Trait) || profile.Trait.StartsWith("Thoroughly Conventional");

            if (!beforeNeedsRomantic && !beforeStaleTrait) continue;

            HeroProfileMapper.UpgradeIfNeeded(profile, hero);
            upgraded++;
         }

         if (upgraded > 0)
            ModLog.Info($"Upgraded {upgraded} legacy profile(s) at session launch.");
      }

      #endregion
   }
}