// Code written by Gabriel Mailhot, 12/07/2026.
// ASSEMBLY pillar, Inc 0 (state layer): holds the single pending assembly and persists it save-safely (JSON in
// a byte[] SyncData key, additive and idempotent, an in-flight assembly survives save/load). Passive for now:
// the convening/travel/presence wiring lands in Inc 1, the round-table session in Inc 2. Never throws from
// SyncData. Pure decisions live in CalradiaRemembers.Logic.Assemblies (RSVP, arrival, progress).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic.Assemblies;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>The campaign-scoped store of the one convened assembly, plus the small API Inc 1 will drive it with.</summary>
    public sealed class AssemblyBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_Assembly_v1bytes";

        public static AssemblyBehavior Instance;

        private AssemblyRecord _pending; // null when nothing is convened

        public AssemblyBehavior()
        {
            Instance = this;
        }

        /// <summary>The convened assembly, or null when none is active.</summary>
        public AssemblyRecord Pending => _pending;

        public bool HasActiveAssembly => _pending != null;

        /// <summary>
        ///   Begins a fresh assembly, replacing any previous one, with each distinct invitee marked Invited.
        ///   The RSVP/travel resolution is Inc 1's job; this only seeds the roster.
        /// </summary>
        public void Begin(string hostSettlementId, string initiatorId, int summonedDay, int deadline, IEnumerable<string> inviteeIds)
        {
            var invitees = new List<AssemblyInviteeRecord>();
            if (inviteeIds != null)
                foreach (string id in inviteeIds)
                    if (!string.IsNullOrEmpty(id) && invitees.All(i => i.HeroId != id))
                        invitees.Add(new AssemblyInviteeRecord {HeroId = id, Status = AssemblyInviteeStatus.Invited});

            _pending = new AssemblyRecord {
                HostSettlementId = hostSettlementId,
                InitiatorId = initiatorId,
                SummonedDay = summonedDay,
                Deadline = deadline,
                Invitees = invitees
            };

            ModLog.Info($"[ASSEMBLY] convened at {hostSettlementId} by {initiatorId}: {invitees.Count} invited, deadline day {deadline}.");
        }

        /// <summary>Sets an invitee's status; no-op when no assembly is active or the hero is not on the roster.</summary>
        public void SetStatus(string heroId, AssemblyInviteeStatus status)
        {
            AssemblyInviteeRecord inv = _pending?.Invitees?.FirstOrDefault(i => i.HeroId == heroId);
            if (inv != null) inv.Status = status;
        }

        /// <summary>The current roster tally (present / in transit / declined / awaiting), zeroed when nothing is convened.</summary>
        public AssemblyProgress Progress()
            => AssemblyProgress.From(_pending?.Invitees?.Select(i => i.Status));

        /// <summary>Cancels the pending assembly. The forced-movement stand-down arrives with the travel slice; here it clears the state.</summary>
        public void Cancel()
        {
            if (_pending != null) ModLog.Info("[ASSEMBLY] cancelled.");
            _pending = null;
        }

        /// <summary>
        ///   An EnRoute invitee has reached the host settlement: seat them as Present. Returns true when one
        ///   advanced. This is the core <see cref="OnSettlementEntered" /> drives, exposed so a test can arrange
        ///   an arrival without moving a party across the whole map.
        /// </summary>
        public bool NoteArrivalAtHost(string heroId)
        {
            AssemblyInviteeRecord inv = _pending?.Invitees?.FirstOrDefault(i => i.HeroId == heroId && i.Status == AssemblyInviteeStatus.EnRoute);

            if (inv == null) return false;

            inv.Status = AssemblyInviteeStatus.Present;
            ModLog.Info($"[ASSEMBLY] {heroId} reached the host and is Present.");

            return true;
        }

        /// <summary>
        ///   At or past the deadline, an invitee still travelling stands in by AMBASSADOR: the assembly is never
        ///   blocked waiting on a party that cannot arrive (the fragile-movement fallback made narrative, Gabriel's
        ///   idea). Exposed so a test can advance the clock without waiting real days. The forced travel that would
        ///   let a lord actually ARRIVE in person is a later, supervised slice (CR's courier scar tissue: forcing a
        ///   lord's party across the map strands and starves it, so it is not switched on lightly).
        /// </summary>
        public void AdvanceArrivals(int today)
        {
            if (_pending?.Invitees == null) return;

            foreach (AssemblyInviteeRecord inv in _pending.Invitees.Where(i => i.Status == AssemblyInviteeStatus.EnRoute).ToList())
                if (today >= _pending.Deadline)
                {
                    inv.Status = AssemblyInviteeStatus.Ambassador;
                    ModLog.Info($"[ASSEMBLY] {inv.HeroId} did not arrive by the deadline; an ambassador stands in.");
                }
        }

        public override void RegisterEvents()
        {
            // Presence: an EnRoute invitee entering the host settlement takes their seat.
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            // The arrival window: at the deadline, anyone still travelling becomes an ambassador.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (_pending == null || settlement == null || settlement.StringId != _pending.HostSettlementId) return;

            Hero arriver = hero ?? party?.LeaderHero;

            if (arriver != null) NoteArrivalAtHost(arriver.StringId);
        }

        private void OnDailyTick() => AdvanceArrivals((int) CampaignTime.Now.ToDays);

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try
                {
                    bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_pending, Formatting.None));
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[ASSEMBLY] serialize failed, persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Array.Empty<byte>();
                }

                dataStore.SyncData(SaveKey, ref bytes);
            }
            else
            {
                byte[] bytes = Array.Empty<byte>();
                try
                {
                    dataStore.SyncData(SaveKey, ref bytes);
                    _pending = bytes is {Length: > 0}
                        ? JsonConvert.DeserializeObject<AssemblyRecord>(Encoding.UTF8.GetString(bytes))
                        : null;
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[ASSEMBLY] load failed, starting with no assembly (graceful degradation). {ex.GetType().Name}: {ex.Message}");
                    _pending = null;
                }
            }
        }
    }
}
