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
using TaleWorlds.Library;

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

        /// <summary>
        ///   Cancels the pending assembly and stands its travellers down: every EnRoute party is handed back to
        ///   its own AI (RethinkAtNextHourlyTick), never left holding our stale move order (the "starving lord"
        ///   scar). Then the state is cleared.
        /// </summary>
        public void Cancel()
        {
            if (_pending == null) return;

            foreach (AssemblyInviteeRecord inv in _pending.Invitees.Where(i => i.Status == AssemblyInviteeStatus.EnRoute))
                ReleaseTravel(Hero.FindFirst(x => x.StringId == inv.HeroId));

            ModLog.Info("[ASSEMBLY] cancelled.");
            _pending = null;
        }

        /// <summary>Orders every EnRoute traveller's party toward the host. Called once at convene and refreshed daily.</summary>
        public void KickTravel()
        {
            if (_pending == null) return;

            Settlement host = Settlement.Find(_pending.HostSettlementId);
            if (host == null) return;

            foreach (AssemblyInviteeRecord inv in _pending.Invitees.Where(i => i.Status == AssemblyInviteeStatus.EnRoute))
                OrderTravel(Hero.FindFirst(x => x.StringId == inv.HeroId), host);
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
            ReleaseTravel(Hero.FindFirst(x => x.StringId == heroId)); // arrived: hand their party back to its own AI
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

        /// <summary>
        ///   Re-checks a convened assembly against the live world and repairs it (Gabriel 2026-07-25): if the
        ///   HOST is no longer viable (razed, besieged, or turned enemy ground) the whole assembly is cancelled
        ///   and the player told why; otherwise each traveller is re-judged by <see cref="AssemblyTravelOutcome" />,
        ///   so a captured or pinned or unreachable member falls back to an ambassador and one who died on the road
        ///   leaves an empty seat. The verdicts are the pure, tested policies; this only reads the campaign for
        ///   them. Runs each day and can be called on the events that could break an assembly.
        /// </summary>
        public void RevalidateNow(int today)
        {
            if (_pending == null) return;

            Settlement host = Settlement.Find(_pending.HostSettlementId);
            Hero initiator = Hero.FindFirst(h => h.StringId == _pending.InitiatorId);

            var hostFacts = new AssemblyHostFacts {
                HostExists = host != null,
                HostBesieged = host != null && host.IsUnderSiege,
                HostOwnerAtWarWithInitiator = HostTurnedHostile(host, initiator)
            };

            HostValidity hostVerdict = AssemblyHostValidity.Evaluate(hostFacts);
            if (hostVerdict != HostValidity.Valid)
            {
                ModLog.Info($"[ASSEMBLY] host no longer viable ({hostVerdict}); cancelling.");
                Notify($"The assembly is called off: {HostReason(hostVerdict)}.");
                Cancel();

                return;
            }

            foreach (AssemblyInviteeRecord inv in _pending.Invitees.Where(i => i.Status == AssemblyInviteeStatus.EnRoute).ToList())
            {
                Hero h = Hero.FindFirst(x => x.StringId == inv.HeroId);
                var facts = new AssemblyTravelFacts {
                    IsAlive = h != null && h.IsAlive,
                    IsPrisoner = h != null && h.IsPrisoner,
                    IsOccupied = IsHeroOccupied(h),
                    CanReachByDeadline = today < _pending.Deadline && h?.PartyBelongedTo != null
                };

                switch (AssemblyTravelOutcome.Evaluate(facts))
                {
                    case TravelOutcome.Fell:
                        ReleaseTravel(h);
                        _pending.Invitees.RemoveAll(i => i.HeroId == inv.HeroId);
                        ModLog.Info($"[ASSEMBLY] {inv.HeroId} fell on the road; their seat is empty.");
                        break;
                    case TravelOutcome.Ambassador:
                        ReleaseTravel(h);
                        inv.Status = AssemblyInviteeStatus.Ambassador;
                        break;
                    default: // Arriving: still on the road, keep nudging their party toward the host (courier pattern).
                        OrderTravel(h, host);
                        break;
                }
            }
        }

        public override void RegisterEvents()
        {
            // Presence: an EnRoute invitee entering the host settlement takes their seat.
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            // The daily re-validation: host still viable? travellers still able? Repairs or cancels as needed.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            if (_pending == null || settlement == null || settlement.StringId != _pending.HostSettlementId) return;

            Hero arriver = hero ?? party?.LeaderHero;

            if (arriver != null) NoteArrivalAtHost(arriver.StringId);
        }

        private void OnDailyTick() => RevalidateNow((int) CampaignTime.Now.ToDays);

        private static bool HostTurnedHostile(Settlement host, Hero initiator)
        {
            IFaction hostFaction = host?.OwnerClan?.MapFaction;
            IFaction initiatorFaction = initiator?.MapFaction;

            return hostFaction != null && initiatorFaction != null && hostFaction.IsAtWarWith(initiatorFaction);
        }

        private static bool IsHeroOccupied(Hero h)
        {
            if (h?.PartyBelongedTo?.MapEvent != null) return true;
            Settlement s = h?.CurrentSettlement;

            return s != null && s.IsUnderSiege;
        }

        private static string HostReason(HostValidity verdict)
        {
            switch (verdict)
            {
                case HostValidity.Gone: return "the host settlement is no more";
                case HostValidity.Besieged: return "the host has fallen under siege";
                case HostValidity.EnemyHeld: return "the host is now in enemy hands";
                default: return "the host is no longer safe";
            }
        }

        private static void Notify(string message)
        {
            try { InformationManager.DisplayMessage(new InformationMessage(message)); }
            catch { /* no UI available (headless / a self-test): the log line already carries it */ }
        }

        /// <summary>
        ///   Nudges a traveller's party toward the host, WITHOUT freezing its AI: we only ever re-issue the goal
        ///   (daily), never lock the party (SetDoNotMakeNewDecisions / a hold order), so it can never be stranded
        ///   the way the "starving lord" bug did. A party too busy to obey is caught at the deadline and falls
        ///   back to an ambassador.
        /// </summary>
        private static void OrderTravel(Hero h, Settlement host)
        {
            MobileParty party = h?.PartyBelongedTo;

            if (party == null || host == null) return;

            try { party.SetMoveGoToSettlement(host, MobileParty.NavigationType.Default, false); }
            catch { /* an odd party state must never break the daily pass */ }
        }

        /// <summary>Hands a party back to its own AI, so it never keeps our stale move order after it leaves the roster.</summary>
        private static void ReleaseTravel(Hero h)
        {
            MobileParty party = h?.PartyBelongedTo;

            if (party?.Ai == null) return;

            try { party.Ai.RethinkAtNextHourlyTick = true; }
            catch { /* releasing is best-effort */ }
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
