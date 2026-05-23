// Code written by Gabriel Mailhot, 19/05/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Dialogue;
using CalradiaRemembers.GameState;
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
        //private const string LogTag = "NPC Memory Service";
        public readonly IGameStateBridge GameStateBridge = new BannerlordGameStateBridge();
        public readonly LlmServices LlmServices = new();

        // ── State ─────────────────────────────────────────────────────────────

        public readonly BannerlordNpcMemoryStore Store = new();
        private readonly LlmDialogFlow _dialogFlow = new();

        public NpcMemoryCampaignBehavior()
        {
            Instance = this;
            LlmServices.Initialize();
        }

        // ── Singleton ─────────────────────────────────────────────────────────

        public static NpcMemoryCampaignBehavior Instance { get; private set; }

        // ── Lifecycle hooks ───────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this, OnSessionLaunched);
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

        // ── Phase 7.2 validation helpers ──────────────────────────────────────

        /// <summary>
        ///   Creates a test NPC profile on first run (empty store).
        ///   On subsequent loads the profile will already be in the store —
        ///   confirming persistence works end-to-end.
        /// </summary>
        private void EnsureTestProfile()
        {
            const string testId = "npc_raganvad_test";

            if (Store.Get(testId) != null)
            {
                ModLog.Info("Test profile found in save — persistence OK.");

                return;
            }

            ModLog.Info("No save data — creating test profile.");

            NpcProfile profile = new() {
                Id = testId,
                Name = "Raganvad (Test)",
                Faction = "Sturgia",
                Clan = "Vagiroving",
                Personality = "Phase 7.2 persistence test."
            };

            profile.Events.Add(new NotableEvent(
                142,
                NotableEventType.FirstMeeting,
                "Player met Raganvad for the first time during Phase 7.2 testing."));

            profile.Events.Add(new NotableEvent(
                142,
                NotableEventType.Agreement,
                "Raganvad agreed to test the persistence system."));

            Store.Set(profile);
            ModLog.Info($"Test profile created ({profile.Events.Count} events). Save the game to persist.");
        }

        // ── Session launched ──────────────────────────────────────────────────

        private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            ModLog.Info("Campaign session launched.");
            EnsureTestProfile();
            DisplayLoadedProfiles();

            _dialogFlow.Register(campaignGameStarter);
            ModLog.Info("Dialog flow registered.");

            VanillaDialogueRecorder.Subscribe();
        }

        #endregion
    }
}