// Code written by Gabriel Mailhot, 19/05/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Commoners;
using CalradiaRemembers.Dialogue;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
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
        public readonly LlmServices LlmServices = new();
        public readonly BannerlordNpcMemoryStore Store = new();

        // ONE QuestDeedTracker, owned by the behavior.
        public readonly QuestDeedTracker QuestTracker = new();

        // ONE LetterTriggerEvaluator, owned by the behavior. Handles tournament/battle
        // event hooks and daily romantic/spouse correspondence checks.
        public readonly LetterTriggerEvaluator LetterEvaluator = new();

        // ONE WorldEventFeeders, owned by the behavior. Records dated world happenings
        // (battles, sieges, marriages, executions, wars) into the WorldEventStore (v1.2).
        public readonly WorldEvents.WorldEventFeeders WorldFeeders = new();

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
        }

        /// <summary>Removes all courier parties just before the world is serialized.</summary>
        private void OnBeforeSave()
            => Letters.CourierService.DespawnAll(Store);

        /// <summary>Restores the in-flight couriers the pre-save despawn removed.</summary>
        private void OnSaveOver(bool isSuccessful, string saveName)
            => Letters.CourierService.RespawnInFlight(Store);

        /// <summary>
        ///   Fires every game hour for each party. Used to deliver courier letters within
        ///   1 hour of arrival rather than waiting for the daily tick.
        /// </summary>
        private void OnHourlyTickParty(MobileParty party)
        {
            Letters.CourierService.CheckCourierArrival(party, Store);
        }

        /// <summary>
        ///   When any mobile party is destroyed, check whether it was a letter courier
        ///   and, if so, mark the carried letter lost (handled in CourierService).
        /// </summary>
        private void OnMobilePartyDestroyed(MobileParty destroyed, PartyBase destroyer)
        {
            Letters.CourierService.HandlePartyDestroyed(destroyed, Store);
        }

        /// <summary>
        ///   When a party enters a settlement, check whether it is a letter courier
        ///   reaching its recipient and, if so, deliver immediately.
        /// </summary>
        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            Letters.CourierService.HandleSettlementEntered(party, settlement, Store);
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

            foreach (var id in ids)
            {
                NpcProfile p = Store.Get(id);

                if (p == null) continue;

                ModLog.Info($"  {p.Name} | {p.Events.Count} event(s) | rep:{p.ReputationWithPlayer}");
                foreach (NotableEvent ev in p.Events)
                    ModLog.Info($"    [{ev.type}] Day {ev.gameDay}: {ev.summary}");
            }
        }


        // ── Session launched ──────────────────────────────────────────────────

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            ModLog.Info("Campaign session launched.");
            CulturalTraditionsLoader.Load();
            NarrativeLoader.Load();
            Jealousy.JealousyCultureLoader.Load();
            LlmServices.Invalidate();   // rebuild PromptBuilder with fresh file content
            StoryLog.WriteSessionHeader();
            CaptiveEncounterService.ResetOnSessionLaunch();
            DisplayLoadedProfiles();
            UpgradeLegacyProfiles();
            RefreshRelationships();
            RumorStore.Clear();
            DialogFlow.Register(campaignGameStarter);
            ModLog.Info("Dialog flow registered.");

            DialogueRecorder.Subscribe();
            QuestTracker.Subscribe();
            LetterEvaluator.Subscribe();
            WorldFeeders.Subscribe();
            new TaverneLetterBehavior().Register(campaignGameStarter);
            new CommonsDialogBehavior().Register(campaignGameStarter);
            CourierService.RebuildActiveCourierSet(Store);
            // Couriers were kept out of the save (despawned pre-save). Don't respawn a fragile
            // party during session launch — instead hand any orphaned in-flight letter back to
            // its date-based delivery, so it still arrives even though the courier is gone.
            CourierService.ClearOrphanedCouriers(Store);
            RemoveOrphanedLetterPlaceholders();
            QuestJournalBridge.ReconcileOnSessionLaunch(Store);
        }

        /// <summary>
        ///   Runs the quest lifecycle pass once per game day: expire overdue quests
        ///   (with a consequence) and cancel ones whose war premise has ended.
        /// </summary>
        private void OnDailyTick()
        {
            int today = GameStateBridge.GetCurrentGameDay();
            QuestService.RunDailyMaintenance(Store, today);
            LetterDeliveryService.CheckDeliveries(Store, today);
            PlayerLetterService.CheckDeliveries(Store, today);
            LetterEvaluator.EvaluateDaily(Store, LlmServices, today);
            TriggerPendingReplies(today);
            SpawnPendingReplyCouriers(today);
            CaptiveEncounterService.CheckAndTrigger(Store, LlmServices);
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

                bool changed = false;
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
                    if (string.IsNullOrWhiteSpace(letter.Content) && letter.DispatchDay == int.MaxValue && !letter.IsDelivered)
                    {
                        orphans.Add(letter);
                        ModLog.Info($"[LETTER] removing orphaned placeholder from {profile.Name} (LLM never responded last session).");
                    }
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

                bool changed = false;

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

        private void RefreshRelationships()
        {
            var refreshed = 0;
            foreach (var id in Store.ListIds())
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

        private void UpgradeLegacyProfiles()
        {
            var upgraded = 0;
            foreach (var id in Store.ListIds())
            {
                NpcProfile profile = Store.Get(id);

                if (profile == null) continue;

                Hero hero = Hero.FindFirst(h => h.StringId == id);

                if (hero == null) continue; // hero gone — skip silently

                var beforeNeedsRomantic = profile.Romantic == null;
                var beforeStaleTrait = string.IsNullOrEmpty(profile.Trait) || profile.Trait.StartsWith("Thoroughly Conventional");

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