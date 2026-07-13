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

        /// <summary>Cancels the pending assembly. Inc 1 will also stand the travellers down; here it only clears the state.</summary>
        public void Cancel()
        {
            if (_pending != null) ModLog.Info("[ASSEMBLY] cancelled.");
            _pending = null;
        }

        public override void RegisterEvents()
        {
            // Inc 0: a passive store. The daily tick (movement + arrival), OnSettlementEntered (presence), and
            // the settlement menus arrive with Inc 1.
        }

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
