// Code written by Gabriel Mailhot, 19/05/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Dialogue;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

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
            ActionVocabularyLoader.Load();
            LlmServices.Invalidate();   // rebuild PromptBuilder with fresh file content
            DisplayLoadedProfiles();
            UpgradeLegacyProfiles();
            RefreshRelationships();
            DialogFlow.Register(campaignGameStarter);
            ModLog.Info("Dialog flow registered.");

            DialogueRecorder.Subscribe();
            QuestTracker.Subscribe();
            LetterEvaluator.Subscribe();
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
            LetterEvaluator.EvaluateDaily(Store, LlmServices, today);
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