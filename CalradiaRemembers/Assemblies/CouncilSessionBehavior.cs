// Code written by Gabriel Mailhot, 27/07/2026.
// Council audit C4: makes an in-flight council survive save/load. The chat window's ledger of pledges used to
// be pure session state that "died with the window", so a save (or a crash, or an autosave) caught mid-sitting
// silently lost every recorded-but-not-yet-lifted pledge. The view-model now registers its live ledger + scope
// here on opening (BeginSession) and clears them on a normal close (EndSession); in between, this behavior owns
// the save contract. On load, if a council was still open at save time its window is gone, so its AGREED
// pledges are settled headlessly through the same CouncilLift the window uses, then the session is cleared.
// Only AGREED pledges commit (AssemblyLedger.PendingCommit), which a normal close would already have turned
// into Applied/Failed, so a reloaded council can never double-execute a pledge the window already settled.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.Assemblies;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>Persists the active council's ledger and settles it on load if its window did not outlive the save.</summary>
    public sealed class CouncilSessionBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilSession_v1bytes";

        public static CouncilSessionBehavior Instance;

        private AssemblyLedger _ledger;
        private CouncilScope? _scope;

        public CouncilSessionBehavior()
        {
            Instance = this;
        }

        /// <summary>The view-model registers the council's live ledger + scope as it opens, so a save mid-sitting persists them.</summary>
        public void BeginSession(CouncilScope scope, AssemblyLedger ledger)
        {
            _scope = scope;
            _ledger = ledger;
        }

        /// <summary>The view-model clears the session on a normal close, its pledges already lifted; nothing is left to recover.</summary>
        public void EndSession()
        {
            _scope = null;
            _ledger = null;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try
                {
                    // Serialize a frozen snapshot only while a council is actually in flight; otherwise persist a
                    // literal "null", so an ordinary save outside a council carries no stale session forward.
                    CouncilSessionSnapshot snapshot = _scope != null && _ledger != null
                        ? new CouncilSessionSnapshot {Scope = _scope.Value, Entries = new List<AssemblyResolutionEntry>(_ledger.Entries ?? new List<AssemblyResolutionEntry>())}
                        : null;
                    bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, Formatting.None));
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[COUNCIL] session serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("null");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[COUNCIL] session byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no in-flight session, never a crash.
                }

                _scope = null;
                _ledger = null;

                if (bytes is {Length: > 0})
                    try
                    {
                        CouncilSessionSnapshot snapshot = JsonConvert.DeserializeObject<CouncilSessionSnapshot>(Encoding.UTF8.GetString(bytes));

                        if (snapshot != null)
                        {
                            var ledger = new AssemblyLedger {Entries = snapshot.Entries ?? new List<AssemblyResolutionEntry>()};
                            ledger.Sanitize(); // repair any blank/duplicate ids a hand-edited save could carry
                            _ledger = ledger;
                            _scope = snapshot.Scope;
                        }
                    }
                    catch (Exception ex)
                    {
                        _scope = null;
                        _ledger = null;
                        ModLog.Error($"[COUNCIL] session load failed, in-flight council dropped for this load. {ex.GetType().Name}: {ex.Message}");
                    }
            }
        }

        /// <summary>One JSON-friendly snapshot of a council in flight: which house met, and its ledger of pledges.</summary>
        public sealed class CouncilSessionSnapshot
        {
            public CouncilScope Scope { get; set; }
            public List<AssemblyResolutionEntry> Entries { get; set; }
        }

        #region private

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Fires once per load/start. A non-null session here can only be one restored from a save made while
            // a council was open: its window is gone, so settle its agreed pledges now and clear.
            if (_scope == null || _ledger == null) return;

            var behavior = NpcMemoryCampaignBehavior.Instance;

            if (behavior?.Store == null || behavior.GameStateBridge == null)
            {
                EndSession();

                return;
            }

            int gameDay = behavior.GameStateBridge.GetCurrentGameDay();

            var settled = 0;
            CouncilLift.Finalize(_ledger, _scope.Value, behavior, gameDay, (message, positive) => settled++);

            if (settled > 0)
            {
                ModLog.Info($"[COUNCIL] recovered {settled} pledge(s) from a council that was open when you saved.");

                try
                {
                    MBInformationManager.AddQuickInformation(
                        new TextObject("The council's decisions, left unsettled when you saved, have now been carried out."));
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[COUNCIL] recovery banner failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            EndSession();
        }

        #endregion
    }
}
