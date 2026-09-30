// Code written by Gabriel Mailhot, 29/08/2026.
// Documents the single decision of how one candidate hero shows up at a council: In Person, Ambassador, or
// Absent. The player complained the war council felt magic (distant kingdom nobility appearing in person); if a
// far own-faction lord resolved to InPerson or Ambassador instead of Absent, the whole point of the convergence
// (Council + Assembly agreeing on ONE seating rule) is lost. Order is load-bearing: Prisoner, then InParty, then
// ParleyTarget, then own-faction willing/near, then everything else is Absent.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilAttendancePolicyTests
    {
        private const double ThresholdDays = 3d;

        // A willing, available, own-faction candidate sitting well inside the threshold: the InPerson baseline.
        // Each test overrides one axis away from this shape.
        private static CouncilAttendanceFacts Facts(bool prisoner = false,
                                                     bool inParty = false,
                                                     bool playerFaction = true,
                                                     bool parleyTarget = false,
                                                     double etaDays = 1d,
                                                     int relation = 0,
                                                     bool atWar = false,
                                                     bool dead = false)
            => new() {
                IsDead = dead, IsPrisoner = prisoner, IsInPlayerParty = inParty, IsPlayerFaction = playerFaction,
                IsParleyTarget = parleyTarget, EtaDays = etaDays, RelationToPlayer = relation,
                AtWarWithPlayer = atWar
            };

        private static CouncilAttendanceResult Decide(CouncilAttendanceFacts f) => CouncilAttendancePolicy.Decide(f, ThresholdDays);

        // ── The dead, before anything else ──────────────────────────────────

        // Maifailun (Nexus, 30/09/2026, on 2.6.6): "my characters dead father is also partaking in the meeting". The
        // game keeps the dead in a clan's list of heroes; a dead lord of the player's own house has no party and no
        // settlement, so his travel estimate reads 0 days, which is exactly "near enough to attend in person".
        [Test]
        public void GIVEN_a_dead_lord_of_the_players_own_house_with_no_whereabouts_WHEN_deciding_THEN_he_never_sits()
        {
            CouncilAttendanceResult r = Decide(Facts(dead: true, playerFaction: true, etaDays: 0d));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.Dead);
        }

        // No seat of any kind: a parley counterpart who has died sends no envoy either.
        [Test]
        public void GIVEN_a_dead_parley_counterpart_WHEN_deciding_THEN_no_envoy_speaks_for_him()
        {
            Decide(Facts(dead: true, playerFaction: false, parleyTarget: true)).Attendance.Should().Be(CouncilAttendance.Absent);
        }

        // ── Prisoner guard ──────────────────────────────────────────────────

        // The most absolute bar: a prisoner cannot attend in any form, even riding in the player's own party or
        // belonging to the player's own faction. If this were checked after InParty, a captured travelling
        // companion would seat as present.
        [Test]
        public void GIVEN_a_prisoner_who_is_also_in_the_players_party_and_own_faction_WHEN_deciding_THEN_prisoner_wins_and_they_are_absent()
        {
            CouncilAttendanceResult r = Decide(Facts(prisoner: true, inParty: true, playerFaction: true));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.Prisoner);
        }

        // ── Party co-location, wins over faction/distance ──────────────────

        // A candidate already riding with the player is present without travel or willingness checks, even when
        // their own faction is far away: co-location overrides the distance/relation checks that would otherwise
        // apply to an own-faction lord.
        [Test]
        public void GIVEN_a_hero_in_the_players_party_WHEN_deciding_THEN_they_attend_in_person_even_if_their_own_faction_is_far()
        {
            CouncilAttendanceResult r = Decide(Facts(inParty: true, playerFaction: true, etaDays: 999d));

            r.Attendance.Should().Be(CouncilAttendance.InPerson);
            r.Reason.Should().Be(CouncilAttendanceReason.InParty);
        }

        // The edge the ratified rules call out explicitly: an other-faction hero who happens to be riding along
        // (e.g. a captive companion released into the party) still attends in person by co-location; being
        // other-faction alone does not force an Ambassador when they are standing right there.
        [Test]
        public void GIVEN_an_other_faction_hero_who_is_in_the_players_party_WHEN_deciding_THEN_party_co_location_still_wins()
        {
            CouncilAttendanceResult r = Decide(Facts(inParty: true, playerFaction: false));

            r.Attendance.Should().Be(CouncilAttendance.InPerson);
            r.Reason.Should().Be(CouncilAttendanceReason.InParty);
        }

        // ── Own faction: distance boundary ──────────────────────────────────

        // The boundary is INCLUSIVE: exactly at the threshold, the journey still counts as reachable.
        [Test]
        public void GIVEN_an_own_faction_lord_whose_eta_sits_exactly_at_the_threshold_WHEN_deciding_THEN_they_attend_in_person()
        {
            CouncilAttendanceResult r = Decide(Facts(etaDays: ThresholdDays));

            r.Attendance.Should().Be(CouncilAttendance.InPerson);
            r.Reason.Should().Be(CouncilAttendanceReason.NearbyOwnFaction);
        }

        // One tick past the threshold and the same lord stays home. Pins the boundary against an off-by-one that
        // would let a lord one day too far still show up.
        [Test]
        public void GIVEN_an_own_faction_lord_whose_eta_is_just_over_the_threshold_WHEN_deciding_THEN_they_are_absent_as_too_far()
        {
            CouncilAttendanceResult r = Decide(Facts(etaDays: ThresholdDays + 0.01d));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.TooFar);
        }

        // The ratified rule this whole increment exists to protect: a far own-faction lord must be Absent, and
        // CRUCIALLY must never resolve to Ambassador. Ambassadors are for other factions only; sending a stand-in
        // for one's own house was exactly the "magic" the player complained about.
        [Test]
        public void GIVEN_a_far_own_faction_lord_WHEN_deciding_THEN_they_are_absent_and_never_an_ambassador()
        {
            CouncilAttendanceResult r = Decide(Facts(etaDays: 999d));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.TooFar);
            r.Attendance.Should().NotBe(CouncilAttendance.Ambassador);
        }

        // ── Own faction: willingness ─────────────────────────────────────────

        // Open war with the player overrides proximity: a near own-faction lord at war does not show up at all.
        [Test]
        public void GIVEN_a_nearby_own_faction_lord_at_open_war_with_the_player_WHEN_deciding_THEN_they_are_absent_as_declined()
        {
            CouncilAttendanceResult r = Decide(Facts(atWar: true));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.Declined);
        }

        // The floor is reused from AssemblyRsvpPolicy.HeedRegardFloor (-20), but read INCLUSIVELY here: regard
        // sitting exactly AT the floor already declines (the ratified wording is "at/below a hostile floor").
        [Test]
        public void GIVEN_a_nearby_own_faction_lord_whose_regard_sits_exactly_at_the_hostile_floor_WHEN_deciding_THEN_they_are_absent_as_declined()
        {
            CouncilAttendanceResult r = Decide(Facts(relation: CouncilAttendancePolicy.HostileRegardFloor));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.Declined);
        }

        // One point above the floor and the same lord is willing again: pins the boundary from the other side.
        [Test]
        public void GIVEN_a_nearby_own_faction_lord_whose_regard_is_just_above_the_hostile_floor_WHEN_deciding_THEN_they_attend_in_person()
        {
            CouncilAttendanceResult r = Decide(Facts(relation: CouncilAttendancePolicy.HostileRegardFloor + 1));

            r.Attendance.Should().Be(CouncilAttendance.InPerson);
            r.Reason.Should().Be(CouncilAttendanceReason.NearbyOwnFaction);
        }

        // ── Parley target: ambassador regardless ────────────────────────────

        // A parley target is represented by an ambassador no matter how far, how hostile, or how openly at war:
        // proximity and relation simply do not apply to them, unlike an own-faction candidate.
        [Test]
        public void GIVEN_a_parley_target_WHEN_deciding_THEN_they_are_represented_by_an_ambassador_regardless_of_distance_or_war()
        {
            CouncilAttendanceResult r = Decide(Facts(playerFaction: false, parleyTarget: true, etaDays: 999d, relation: -100, atWar: true));

            r.Attendance.Should().Be(CouncilAttendance.Ambassador);
            r.Reason.Should().Be(CouncilAttendanceReason.ParleyEnvoy);
        }

        // ── Other faction, not sought for a parley ──────────────────────────

        // An other-faction hero who is not a parley target is not a council candidate at all: absent, not an
        // ambassador, since only a specifically-sought parley representative ever gets a stand-in.
        [Test]
        public void GIVEN_an_other_faction_hero_not_sought_for_a_parley_WHEN_deciding_THEN_they_are_absent_as_not_invited()
        {
            CouncilAttendanceResult r = Decide(Facts(playerFaction: false, parleyTarget: false));

            r.Attendance.Should().Be(CouncilAttendance.Absent);
            r.Reason.Should().Be(CouncilAttendanceReason.OtherFactionNotInvited);
        }
    }
}
