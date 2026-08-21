// Code written by Gabriel Mailhot, 10/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Commoners;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Dialogue;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Missions;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
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

      // ONE durable conversation-summary queue, owned by the behavior. Conversation recaps are enqueued here
      // instead of fired as a detached Task, so a summary survives a save/quit/crash and is replayed on load.
      public readonly ConversationSummaryQueueRunner ConvSummaryQueue = new();

      // ONE QuestDeedTracker, owned by the behavior.
      public readonly QuestDeedTracker QuestTracker = new();
      public readonly BannerlordNpcMemoryStore Store = new();

      // ONE WorldEventFeeders, owned by the behavior. Records dated world happenings
      // (battles, sieges, marriages, executions, wars) into the WorldEventStore (v1.2).
      public readonly WorldEventFeeders WorldFeeders = new();

      // ONE WorldConditionsFeeder, owned by the behavior. On a weekly cadence, scans for standing world
      // conditions with no vanilla event of their own (war looming, town hardship) and records them into the
      // same WorldEventStore pipeline, so they surface as rumour / "what you've heard" / chronicle uniformly.
      public readonly WorldConditionsFeeder WorldConditions = new();

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
         // Captivity escape: after the captive chat closes on a "break for it", stage the real escape mission
         // on the next frame (a mission cannot open from inside the chat screen). Per-frame, cheap when idle.
         CampaignEvents.TickEvent.AddNonSerializedListener(
            this, Duels.CaptiveEscapeCoordinator.OnTick);
         // Duels: same deferred-launch shape as the escape coordinator above, for a duel armed in chat.
         CampaignEvents.TickEvent.AddNonSerializedListener(
            this, Duels.DuelCoordinator.OnTick);
         // Courier audit 3.1: drain the letter-mutation queue on the main thread. The LLM letter generators
         // resume on the thread pool and only ENQUEUE their SentLetters/store mutations; this applies them
         // here, in the single-threaded world the daily/courier ticks enumerate.
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(
            this, OnHourlyTick);
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
      ///   Called by Bannerlord on every save and load.
      ///   We delegate entirely to the store — it handles both directions.
      /// </summary>
      public override void SyncData(IDataStore dataStore)
      {
         Store.SyncData(dataStore);
         // Additive, distinct key: the durable conversation-summary queue. Its own load degrades to empty, so a
         // save written before this feature (no such key) opens fine.
         ConvSummaryQueue.SyncData(dataStore);
         // B1: persisted subject/global correspondence-interest dampers. Same additive/graceful contract.
         LetterEvaluator.SyncData(dataStore);
      }

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
      ///   Fires every game hour for each party. Used to deliver courier letters within
      ///   1 hour of arrival rather than waiting for the daily tick.
      /// </summary>
      private void OnHourlyTickParty(MobileParty party)
      {
         CourierService.CheckCourierArrival(party, Store);
      }

      /// <summary>
      ///   Applies any letter-generation mutations the LLM continuations queued off-thread (courier audit 3.1).
      ///   Runs on the main thread, so the SentLetters/store writes never race the ticks that read them.
      /// </summary>
      private void OnHourlyTick()
      {
         SafeRun.Step("OnHourlyTick.LetterMutationQueue.Drain", Letters.LetterMutationQueue.DrainOnMainThread);
      }

      /// <summary>
      ///   When any mobile party is destroyed, check whether it was a letter courier
      ///   and, if so, mark the carried letter lost (handled in CourierService).
      /// </summary>
      private void OnMobilePartyDestroyed(MobileParty destroyed, PartyBase destroyer)
      {
         // 1.10: pass the destroyer so a non-bandit loss is not misreported as a bandit waylay. 1.11: guard so a
         // failure here never breaks the engine's own party-destruction pipeline.
         if (destroyed == null) return;
         SafeRun.Step("OnMobilePartyDestroyed.HandlePartyDestroyed",
            () => CourierService.HandlePartyDestroyed(destroyed, Store, destroyer));
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
         SafeRun.Step("OnSessionLaunched.LetterWindowManager.ResetForSession", Letters.LetterWindowManager.ResetForSession); // C4: never inherit a dead window from a prior session
         SafeRun.Step("OnSessionLaunched.LetterMutationQueue.Clear", Letters.LetterMutationQueue.Clear); // 3.1: drop closures captured against a prior campaign's objects
         SafeRun.Step("OnSessionLaunched.DisplayLoadedProfiles", DisplayLoadedProfiles);
         SafeRun.Step("OnSessionLaunched.UpgradeLegacyProfiles", UpgradeLegacyProfiles);
         // Replay any conversation-summary jobs that survived the load (SyncData(load) ran first, so the queue
         // is already rehydrated). Never clear it here: unlike the letter-mutation queue's stale closures, these
         // are plain data loaded from THIS save and are meant to be finished.
         SafeRun.Step("OnSessionLaunched.ConvSummaryQueue.ReplayOnLoad", ConvSummaryQueue.ReplayOnLoad);
         // Heals saves made before the inline chain-of-thought fix (ReasoningTraceStripper): stored
         // memories that ARE the model's leaked reasoning are scrubbed or replaced by a "faded" line.
         // Idempotent by construction, so it simply runs on every launch (a clean profile is a no-op).
         SafeRun.Step("OnSessionLaunched.ScrubLeakedReasoningTraces", ScrubLeakedReasoningTraces);
         SafeRun.Step("OnSessionLaunched.RefreshRelationships", RefreshRelationships);
         SafeRun.Step("OnSessionLaunched.RumorStore.Clear", RumorStore.Clear);
         SafeRun.Step("OnSessionLaunched.DialogFlow.Register", () => DialogFlow.Register(campaignGameStarter));
         // The staged-captive intercept line: while a captive scene is staging its conversation, this wins the
         // "start" token (so no vanilla "Who are you?" intro shows) and hands the conversation to the captive chat.
         SafeRun.Step("OnSessionLaunched.CaptiveSceneStager.Register", () => CaptiveSceneStager.Register(campaignGameStarter));
         // Same staging, for the council: while cr.council is staging its conversation scene, this line wins
         // the "start" token and hands the conversation to the council once its scene has loaded.
         SafeRun.Step("OnSessionLaunched.CouncilConversationStager.Register", () => CouncilConversationStager.Register(campaignGameStarter));
         // Nemesis pillar increment 2 (R2 execute / R3 torture, chat-driven entry point): while the player
         // holds a tracked nemesis prisoner, an extra hero_main_options line (reached from the party-screen
         // Talk on him, same hub as any other hero) opens the captor scene against him. Console: cr.nemesis_captor_scene / cr.execute_prisoner.
         SafeRun.Step("OnSessionLaunched.NemesisPrisonerDialogFlow.Register", () => NemesisPrisonerDialogFlow.Register(campaignGameStarter));
         // Nemesis pillar increment 3 (bounty missions, the world hook): an eligible lord (own region
         // matches a living, unresolved, not-already-bountied nemesis) offers to pay for his capture or
         // death, reached from the same hero_main_options hub. Console: cr.nemesis_bounty / cr.nemesis_bounty_complete.
         SafeRun.Step("OnSessionLaunched.NemesisBountyDialogFlow.Register", () => NemesisBountyDialogFlow.Register(campaignGameStarter));
         SafeRun.Step("OnSessionLaunched.NemesisTavernRumorDialogFlow.Register", () => NemesisTavernRumorDialogFlow.Register(campaignGameStarter));
         ModLog.Info("Dialog flow registered.");

         SafeRun.Step("OnSessionLaunched.DialogueRecorder.Subscribe", DialogueRecorder.Subscribe);
         SafeRun.Step("OnSessionLaunched.QuestTracker.Subscribe", QuestTracker.Subscribe);
         // Save-repair: credit any outstanding capture/attack quest whose target the player already holds
         // prisoner (e.g. a save from a build that did not stamp AttackLord on capture). See the method's doc.
         SafeRun.Step("OnSessionLaunched.QuestTracker.ReconcileHeldTargetsOnLoad", QuestTracker.ReconcileHeldTargetsOnLoad);
         SafeRun.Step("OnSessionLaunched.LetterEvaluator.Subscribe", LetterEvaluator.Subscribe);
         SafeRun.Step("OnSessionLaunched.WorldFeeders.Subscribe", WorldFeeders.Subscribe);
         SafeRun.Step("OnSessionLaunched.WorldConditions.Subscribe", WorldConditions.Subscribe);
         SafeRun.Step("OnSessionLaunched.TaverneLetterBehavior.Register", () => new TaverneLetterBehavior().Register(campaignGameStarter));
         SafeRun.Step("OnSessionLaunched.CommonsDialogBehavior.Register", () => new CommonsDialogBehavior().Register(campaignGameStarter));
         SafeRun.Step("OnSessionLaunched.CourierService.RebuildActiveCourierSet", () => CourierService.RebuildActiveCourierSet(Store));
         SafeRun.Step("OnSessionLaunched.CourierSpeedPatch.EnsureFloorOnActiveModel", CourierSpeedPatch.EnsureFloorOnActiveModel); // attach the courier speed floor to the live model (works under War Sails), warn only if truly unreachable
         // Couriers were kept out of the save (despawned pre-save). Don't respawn a fragile
         // party during session launch: instead hand any orphaned in-flight letter back to
         // its date-based delivery, so it still arrives even though the courier is gone.
         SafeRun.Step("OnSessionLaunched.CourierService.ClearOrphanedCouriers", () => CourierService.ClearOrphanedCouriers(Store));
         SafeRun.Step("OnSessionLaunched.RemoveOrphanedLetterPlaceholders", RemoveOrphanedLetterPlaceholders);
         SafeRun.Step("OnSessionLaunched.QuestJournalBridge.ReconcileOnSessionLaunch", () => QuestJournalBridge.ReconcileOnSessionLaunch(Store));
         SafeRun.Step("OnSessionLaunched.CompanionErrandJournalBridge.ReconcileOnSessionLaunch", CompanionErrandJournalBridge.ReconcileOnSessionLaunch);
         SafeRun.Step("OnSessionLaunched.CommitmentJournalBridge.ReconcileOnSessionLaunch", CommitmentJournalBridge.ReconcileOnSessionLaunch);
         // Grudges: the one-shot, idempotent backfill (safe to run on every launch — a versioned no-op
         // once already migrated). Runs last, after every store it scans (Store, WorldEventStore, the
         // Bastards/Divorce markers) is guaranteed loaded.
         SafeRun.Step("OnSessionLaunched.GrudgeBehavior.RunBackfillIfNeeded", () => GrudgeBehavior.Instance?.RunBackfillIfNeeded(Store));
         // Appreciations: the same one-shot, idempotent backfill discipline as Grudges, run right after it.
         SafeRun.Step("OnSessionLaunched.AppreciationBehavior.RunBackfillIfNeeded", () => AppreciationBehavior.Instance?.RunBackfillIfNeeded(Store));
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
      ///   One pass over every stored profile through <see cref="ReasoningTraceBackfill" />: memories that
      ///   are really a reasoning model's leaked chain-of-thought (saves made before the parse-time strip)
      ///   are healed in place. Only profiles that actually changed are re-persisted. Public so the
      ///   reasoning_scrub self-test can drive the exact launch step against a seeded store.
      /// </summary>
      public void ScrubLeakedReasoningTraces()
      {
         var scrubbed = 0;
         foreach (string id in Store.ListIds())
         {
            NpcProfile profile = Store.Get(id);

            if (profile == null) continue;
            if (!ReasoningTraceBackfill.Scrub(profile)) continue;

            Store.Set(profile);
            scrubbed++;
         }

         if (scrubbed > 0)
            ModLog.Info($"Scrubbed leaked reasoning traces from {scrubbed} profile(s).");
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

            // Courier audit (island report): ONE courier in flight per sender. Without this, every queued
            // letter dispatched its own courier and slow or stuck couriers snowballed into a pack riding
            // from the same NPC. The queued letters wait; each spawns its courier once the road is clear.
            if (CourierDispatchPolicy.HasCourierInFlight(profile.SentLetters)) continue;

            var changed = false;

            foreach (PendingLetter letter in profile.SentLetters)
            {
               if (letter.IsDelivered || letter.IsLost) continue;
               if (!string.IsNullOrEmpty(letter.CourierPartyId)) continue; // already dispatched
               if (letter.DispatchDay == 0 || today < letter.DispatchDay) continue;

               Hero sender = Hero.FindFirst(h => h.StringId == profile.Id);

               if (sender == null) continue;

               // The letter dies with its author (player report: a wife dead in childbirth kept "writing"
               // her queued child-support demand). Marking it lost also heals older saves on this tick.
               if (!sender.IsAlive)
               {
                  letter.IsLost = true;
                  changed = true;
                  ModLog.Info($"[LETTER] dropped pending letter from the late {profile.Name} (died before dispatch).");

                  continue;
               }

               // A child-support demand from the player's own spouse is never legitimate: the child is shared and
               // kept in the household, not a debt collected by courier (player report 2026-07-10, married mother
               // in the player's party still demanding support). Dropping it here also heals saves where such a
               // letter was queued before the marriage.
               if (letter.Reason == LetterReason.ChildSupportRequest && sender == Hero.MainHero?.Spouse)
               {
                  letter.IsLost = true;
                  changed = true;
                  ModLog.Info($"[LETTER] dropped child-support demand from the player's spouse {profile.Name} (shared child, not a debt).");

                  continue;
               }

               // Only ADULTS initiate correspondence: a child's queued letter is dropped (heals existing saves,
               // the queue-time gate refuses new ones). A child may still reply to a letter the player sent them.
               if (sender.IsChild && letter.Reason != LetterReason.PlayerLetterReply)
               {
                  letter.IsLost = true;
                  changed = true;
                  ModLog.Info($"[LETTER] dropped queued letter from the child {profile.Name} ({letter.Reason}): children initiate no letters.");

                  continue;
               }

               // A sender who is effectively BESIDE the player right now (same party, same army, same town or
               // castle) does not post a letter to them (player report: a lady in the player's army sent "I miss
               // you" letters). The letter is not lost, only DEFERRED: the courier departs on a later tick, once
               // they are truly apart. Also heals letters already queued on existing saves.
               if (HeroProximity.IsNearPlayer(sender))
               {
                  ModLog.Info($"[LETTER] deferred dispatch from {profile.Name} ({letter.Reason}): they are with the player right now.");

                  continue;
               }

               if (CourierService.TrySpawnReplyCourier(letter, sender, escorted: profile.EscortedChannel))
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

               // 1.8 (courier audit): resolve the hero BEFORE consuming the trigger. Marking HasTriggeredReply
               // first meant a hero who has since gone/died had their reply silently swallowed (flagged triggered,
               // never generated). A vanished hero simply leaves the letter un-triggered, free to trigger later.
               Hero hero = Hero.FindFirst(h => h.StringId == profile.Id);
               if (hero is not {IsAlive: true}) continue;

               letter.HasTriggeredReply = true;
               changed = true;

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
            bool beforeStaleTrait = profile.Trait != null && (string.IsNullOrEmpty(profile.Trait) || profile.Trait.StartsWith("Thoroughly Conventional"));

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