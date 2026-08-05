// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 1 (COUNCIL_ACTIONS.md's lend_troops, Gabriel's own CONDITIONS, the
// highest-detail item in the spec): the FIRST council executor whose deed does not settle at the lift itself.
// Gabriel's condition A ("il ne faut pas qu'un Lord du conseil se deplace lui-meme... il envoie une delegation")
// makes this an ETALED (delayed) executor: the lift only STARTS the delegation (TryStart, called from
// CouncilLift.SettleLendTroops), this behavior then steers a real MobileParty toward the player each day and
// transfers the troops (PERMANENTLY, condition B) on arrival. Mirrors CompanionMissionBehavior's own spawn +
// travel + arrival shape (see that file's own header) and CouncilStipendBehavior's own SyncData discipline (byte[]
// JSON via Newtonsoft.Json, idempotent load, graceful on missing/corrupt, brand-new additive save key, nothing
// else touched) - simplified to a single Outbound leg (no Lingering/Returning/AwaitingReunion): a delegation has
// no destination task and no identity to protect, it only ever rides toward the player once and is done.
//
// DESIGN CALL (documented loudly per the task brief): Kimi's review (COUNCIL_ACTIONS.md) offers two fallbacks for
// "no minor hero of the donor's own house is free to lead the escort" - instant delivery at the lift with an RP
// justification, or refuse the resolution outright. This build picks INSTANT DELIVERY (TryStart falls through to
// TroopDeliveryOutcome.InstantFallback): most lords in a live campaign are lone nobles with no spare kin/companion
// sitting idle, so refusing outright would make lend_troops nearly always fail exactly where it is most wanted (a
// lone vassal supporting the player's war effort - Gabriel's own example scenario). CouncilLift reports the
// instant case with an explicit RP note ("his men ride through the night and reach you already") so the player is
// never told a delegation is travelling when none exists - never a silent substitution.
#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Assemblies;
using CalradiaRemembers.Travel;
using CalradiaRemembers.WorldEvents;
using Helpers;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Assemblies
{
    /// <summary>
    ///   The three shapes CouncilLift.SettleLendTroops must report differently: a real delegation now travelling
    ///   (Applied only once it arrives - the lift itself reports "on the way"), an instant fallback delivery
    ///   (Applied immediately, with an RP note), or a refusal (no troops moved at all).
    /// </summary>
    public enum TroopDeliveryOutcome
    {
        Delegation,
        InstantFallback,
        Failed
    }

    /// <summary>Persists in-flight lend_troops delegations and advances each daily toward the player.</summary>
    public sealed class CouncilTroopDeliveryBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "CalradiaRemembers_CouncilTroopDeliveries_v1bytes";

        // Mirrors CompanionMissionBehavior's own ArrivalDistance - close enough to the player to count as "arrived".
        private const float ArrivalDistance = 2.0f;

        public static CouncilTroopDeliveryBehavior Instance;

        private List<CouncilTroopDelivery> _active = new List<CouncilTroopDelivery>();

        public CouncilTroopDeliveryBehavior()
        {
            Instance = this;
        }

        public int ActiveCount => _active.Count;

        private static int Today => (int) CampaignTime.Now.ToDays;

        /// <summary>
        ///   Starts a lend_troops delivery for <paramref name="loan" /> (already computed fresh from the donor's
        ///   LIVE roster by CouncilLift, "the bridge is law" - this method never re-derives the split, only moves
        ///   it). Spawns a delegation led by a free minor hero of the donor's own clan when one exists (never the
        ///   donor himself - Gabriel's condition A); otherwise falls back to an INSTANT transfer (this file's own
        ///   header documents the choice). <paramref name="moved" /> is the ACTUAL count transferred (re-clamped
        ///   against the live roster at the moment of the move, may be less than <paramref name="loan" /> if the
        ///   party shrank between the lift's own computation and this call).
        /// </summary>
        public TroopDeliveryOutcome TryStart(Hero donor, TierTroopCounts loan, out TierTroopCounts moved, out string failReason)
        {
            moved = new TierTroopCounts(0, 0, 0);
            failReason = null;

            if (donor is not {IsAlive: true} || donor.IsPrisoner)
            {
                failReason = "they could not be found.";

                return TroopDeliveryOutcome.Failed;
            }

            MobileParty donorParty = donor.PartyBelongedTo;

            if (donorParty == null || donorParty.LeaderHero != donor)
            {
                failReason = "they no longer lead a party of their own.";

                return TroopDeliveryOutcome.Failed;
            }

            if (loan.Total <= 0)
            {
                failReason = "they have no troops left to spare.";

                return TroopDeliveryOutcome.Failed;
            }

            Hero escortLeader = FindEscortLeader(donor);
            MobileParty escortParty = null;

            if (escortLeader != null)
                try { escortParty = MobilePartyHelper.CreateNewClanMobileParty(escortLeader, donor.Clan); }
                catch (Exception ex) { ModLog.Error($"[COUNCIL] lend_troops escort spawn failed for {donor.Name}: {ex.GetType().Name}: {ex.Message}"); }

            if (escortParty != null)
            {
                moved = TroopTierBucketer.MoveLoan(donorParty, escortParty, loan);

                if (moved.Total <= 0)
                {
                    TravelingParty.Destroy(escortParty);
                    failReason = "their party had nothing left to send by the time the delegation could set out.";

                    return TroopDeliveryOutcome.Failed;
                }

                SteerTowardPlayer(escortParty);

                _active.Add(new CouncilTroopDelivery {
                    Id = Guid.NewGuid().ToString("N"),
                    DonorHeroId = donor.StringId,
                    EscortLeaderId = escortLeader.StringId,
                    PartyId = escortParty.StringId,
                    LowCount = moved.Low,
                    MediumCount = moved.Medium,
                    HighCount = moved.High
                });

                ModLog.Info($"[COUNCIL] lend_troops: {donor.Name} sends a delegation led by {escortLeader.Name} with {moved.Total} troops ({moved.Low}/{moved.Medium}/{moved.High}).");

                return TroopDeliveryOutcome.Delegation;
            }

            // FALLBACK (this file's own header): no minor hero of the donor's own house was free to lead the
            // escort. Deliver instantly rather than refuse the whole pledge.
            if (MobileParty.MainParty == null)
            {
                failReason = "there is nowhere to send them right now.";

                return TroopDeliveryOutcome.Failed;
            }

            moved = TroopTierBucketer.MoveLoan(donorParty, MobileParty.MainParty, loan);

            if (moved.Total <= 0)
            {
                failReason = "their party had nothing left to send by the time the pledge was carried out.";

                return TroopDeliveryOutcome.Failed;
            }

            ModLog.Info($"[COUNCIL] lend_troops: no minor hero of {donor.Clan?.Name}'s own house was free to lead a delegation for {donor.Name} - falling back to instant delivery ({moved.Total} troops, {moved.Low}/{moved.Medium}/{moved.High}).");

            return TroopDeliveryOutcome.InstantFallback;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                byte[] bytes;
                try { bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_active, Formatting.None)); }
                catch (Exception ex)
                {
                    ModLog.Error($"[COUNCIL] lend_troops delivery serialize failed - persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    bytes = Encoding.UTF8.GetBytes("[]");
                }

                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch (Exception ex) { ModLog.Error($"[COUNCIL] lend_troops delivery byte[] save failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                byte[] bytes = null;
                try { dataStore.SyncData(SaveKey, ref bytes); }
                catch
                {
                    // Key absent in a save written before this behavior existed: brand new, additive key, so it
                    // degrades to no in-flight deliveries, never a crash.
                }

                if (bytes is {Length: > 0})
                    try { _active = JsonConvert.DeserializeObject<List<CouncilTroopDelivery>>(Encoding.UTF8.GetString(bytes)) ?? new List<CouncilTroopDelivery>(); }
                    catch (Exception ex)
                    {
                        _active = new List<CouncilTroopDelivery>();
                        ModLog.Error($"[COUNCIL] lend_troops delivery load failed - dropping any in-flight delegations so the campaign opens. {ex.GetType().Name}: {ex.Message}");
                    }
                else
                    _active = new List<CouncilTroopDelivery>();

                ModLog.Info($"[COUNCIL] loaded {_active.Count} in-flight lend_troops delivery(s) from save.");
            }
        }

        #region private

        /// <summary>Advances one delivery a day. Returns true when it is finished (arrived, lost, or turned back) and should be dropped.</summary>
        private bool Advance(CouncilTroopDelivery d)
        {
            MobileParty party = FindParty(d.PartyId);

            if (party == null)
            {
                // Kimi review (COUNCIL_ACTIONS.md): the escort can be intercepted and destroyed. The donor tried
                // in good faith - a world-event note, but NEVER a grudge or relation hit against them.
                RecordEscortLost(d);

                return true;
            }

            Hero donor = HeroById(d.DonorHeroId);

            // Kimi review: war declared between donor and player mid-transit - the delegation must not deliver
            // troops to a faction now at war with its own donor. Turns back rather than completing the gift.
            if (donor?.MapFaction != null && Hero.MainHero?.MapFaction != null &&
                FactionManager.IsAtWarAgainstFaction(donor.MapFaction, Hero.MainHero.MapFaction))
            {
                TravelingParty.Destroy(party);
                ModLog.Info($"[COUNCIL] lend_troops delegation from {donor.Name} turned back: war broke out before it could arrive.");

                return true;
            }

            if (MobileParty.MainParty == null) return false; // nowhere to steer toward yet - wait a tick

            if (NearPlayer(party))
            {
                DeliverAndDisband(d, party, donor);

                return true;
            }

            TravelingParty.DirectToMovingPoint(party, MobileParty.MainParty.GetPosition2D);

            return false;
        }

        private static bool NearPlayer(MobileParty party)
            => MobileParty.MainParty != null && party.GetPosition2D.Distance(MobileParty.MainParty.GetPosition2D) < ArrivalDistance;

        private void DeliverAndDisband(CouncilTroopDelivery d, MobileParty party, Hero donor)
        {
            // Re-bucket the escort's OWN live roster at arrival rather than trusting the counts recorded at
            // departure ("the bridge is law" applied a second time): a random encounter en route could have
            // thinned the escort without destroying it outright.
            TierTroopCounts carried = TroopTierBucketer.BucketHealthyTroops(party.MemberRoster);
            TroopTierBucketer.MoveLoan(party, MobileParty.MainParty, carried);
            TravelingParty.Destroy(party);

            string donorName = donor?.Name?.ToString() ?? "A lord";
            MBInformationManager.AddQuickInformation(
                new TextObject($"{donorName}'s delegation has reached you with {carried.Total} soldiers."),
                0, donor?.CharacterObject);
            ModLog.Info($"[COUNCIL] lend_troops delivered: {donorName} -> player, {carried.Total} troops ({carried.Low}/{carried.Medium}/{carried.High}).");
        }

        /// <summary>Finds a free minor hero of the donor's own clan to lead the delegation - never the donor, never the player, never someone already leading/riding in a party of their own.</summary>
        private static Hero FindEscortLeader(Hero donor)
        {
            Clan clan = donor.Clan;
            if (clan?.Heroes == null) return null;

            return clan.Heroes.FirstOrDefault(h =>
                h != donor && h != Hero.MainHero && h is {IsAlive: true} && !h.IsPrisoner && h.PartyBelongedTo == null);
        }

        private static MobileParty FindParty(string id)
            => string.IsNullOrEmpty(id)
                ? null
                : MobileParty.All.FirstOrDefault(p => p.StringId == id);

        private static Hero HeroById(string id)
            => string.IsNullOrEmpty(id)
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);

        private void OnDailyTick()
        {
            if (_active.Count == 0) return;

            foreach (CouncilTroopDelivery d in _active.ToList())
            {
                CouncilTroopDelivery delivery = d;
                // One delivery's advance throwing must not stall every other delivery's journey this tick,
                // mirroring CompanionMissionBehavior.OnDailyTick's own SafeRun.Step wrapping.
                SafeRun.Step($"CouncilTroopDeliveryBehavior.Advance({delivery.Id})", () =>
                {
                    if (Advance(delivery))
                        _active.Remove(delivery);
                });
            }
        }

        /// <summary>Kimi review (COUNCIL_ACTIONS.md): an escort destroyed en route is a WORLD EVENT, never a grudge against the donor who tried in good faith.</summary>
        private void RecordEscortLost(CouncilTroopDelivery d)
        {
            Hero donor = HeroById(d.DonorHeroId);
            string donorName = donor?.Name?.ToString() ?? "A lord";
            int total = d.LowCount + d.MediumCount + d.HighCount;
            string playerName = Hero.MainHero?.Name?.ToString() ?? "the player";

            WorldEventStore.Instance?.Record(new WorldEvent {
                Type = WorldEventType.Battle,
                Day = Today,
                InvolvesPlayer = true,
                Magnitude = 20,
                Summary = $"{donorName}'s delegation of {total} soldiers, riding to reinforce {playerName}, was cut down on the road.",
                ActorIds = donor != null ? new List<string> {donor.StringId} : new List<string>(),
                FactionIds = donor?.MapFaction != null ? new List<string> {donor.MapFaction.StringId} : new List<string>(),
                ChronicleLine = $"{donorName} sent {total} soldiers to your aid, but they never arrived - lost on the road."
            });

            ModLog.Info($"[COUNCIL] lend_troops delegation from {donorName} was lost en route (no fault of the donor - no grudge).");
        }

        private static void SteerTowardPlayer(MobileParty party)
        {
            if (MobileParty.MainParty != null) TravelingParty.DirectToMovingPoint(party, MobileParty.MainParty.GetPosition2D);
        }

        #endregion
    }
}
