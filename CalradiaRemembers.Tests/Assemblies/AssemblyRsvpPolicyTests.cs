// Code written by Gabriel Mailhot, 12/07/2026.
// Documents who accepts a summons to an assembly and why they refuse. Order of precedence is load-bearing:
// availability, then the ground (no one enters an enemy's town), then willingness, then relevance. The
// thresholds here are the tuning knobs; these tests pin the decision SHAPE, not the exact numbers.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class AssemblyRsvpPolicyTests
    {
        // A willing, available, nearby ally with neutral regard: the Accept baseline. Each test overrides one axis.
        private static AssemblyInvitationFacts Facts(
            bool alive = true, bool prisoner = false, bool occupied = false,
            bool enemyGround = false, int regard = 0, bool parley = false,
            int distance = 0, bool companion = false)
            => new() {
                IsAlive = alive, IsPrisoner = prisoner, IsOccupied = occupied,
                HostOwnedByInviteesEnemy = enemyGround, RegardToInitiator = regard,
                InitiatorAtWarWithInvitee = parley, DistanceTier = distance,
                IsCompanionOfInitiator = companion
            };

        private static RsvpOutcome Decide(AssemblyInvitationFacts f) => AssemblyRsvpPolicy.Decide(f);

        // ── Shortcut ────────────────────────────────────────────────────────

        // A companion travels with the initiator already, so summoning them is not a real invitation: no RSVP,
        // no journey. This pins that the shortcut overrides every other gate, even distance and enemy ground.
        [Test]
        public void GIVEN_a_companion_already_in_the_party_WHEN_deciding_THEN_they_are_accepted_even_if_far_or_cool()
        {
            RsvpOutcome o = Decide(Facts(companion: true, distance: 9, regard: -50, enemyGround: true));

            o.Accepted.Should().BeTrue();
            o.Reason.Should().Be(RsvpReason.Accepted);
        }

        // ── Availability ────────────────────────────────────────────────────

        // The core design constraint: a player cannot magically summon everyone. A dead invitee is the most
        // basic case a summons must not paper over.
        [Test]
        public void GIVEN_a_dead_invitee_WHEN_deciding_THEN_it_declines_as_unavailable()
            => Decide(Facts(alive: false)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        // A prisoner cannot even consider the summons, whatever their regard for the summoner.
        [Test]
        public void GIVEN_an_imprisoned_invitee_WHEN_deciding_THEN_it_declines_as_unavailable()
            => Decide(Facts(prisoner: true)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        // A hero pinned by a siege or an active battle cannot leave to travel, regardless of willingness.
        [Test]
        public void GIVEN_an_invitee_pinned_by_a_siege_or_battle_WHEN_deciding_THEN_it_declines_as_unavailable()
            => Decide(Facts(occupied: true)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        // Order matters (per the policy's own header): availability gates FIRST. A prisoner on enemy ground must
        // still read as "unavailable", not "enemy ground", or the reason letter would misreport why they can't come.
        [Test]
        public void GIVEN_a_prisoner_on_enemy_ground_WHEN_deciding_THEN_availability_wins_over_the_ground_reason()
            => Decide(Facts(prisoner: true, enemyGround: true)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        // ── Ground ──────────────────────────────────────────────────────────

        // No one walks into the territory of a faction they are at war with, however much they like the
        // summoner personally: this is the emergent "a parley needs neutral ground" constraint from the design.
        [Test]
        public void GIVEN_a_host_owned_by_the_invitees_enemy_WHEN_deciding_THEN_it_declines_for_enemy_ground_even_when_liked()
            => Decide(Facts(enemyGround: true, regard: 80)).Reason.Should().Be(RsvpReason.DeclinedEnemyGround);

        // Ground is checked before willingness: an enemy-owned host must report as "enemy ground", not
        // "hostile", so the refusal reads as territorial rather than personal.
        [Test]
        public void GIVEN_enemy_ground_and_a_hostile_regard_WHEN_deciding_THEN_the_ground_reason_wins_over_hostility()
            => Decide(Facts(enemyGround: true, regard: -90)).Reason.Should().Be(RsvpReason.DeclinedEnemyGround);

        // ── Willingness ─────────────────────────────────────────────────────

        // The boundary must be INCLUSIVE at the floor. One point below HeedRegardFloor and the invitee does not
        // care enough about the summoner to heed the call.
        [Test]
        public void GIVEN_an_ally_whose_regard_is_below_the_heed_floor_WHEN_deciding_THEN_it_declines_as_hostile()
            => Decide(Facts(regard: AssemblyRsvpPolicy.HeedRegardFloor - 1)).Reason.Should().Be(RsvpReason.DeclinedHostile);

        // Paired with the test above: sitting exactly on HeedRegardFloor still accepts, fixing the line so a
        // future retune of the constant cannot silently move who gets heard.
        [Test]
        public void GIVEN_an_ally_whose_regard_sits_exactly_at_the_heed_floor_WHEN_deciding_THEN_it_accepts()
            => Decide(Facts(regard: AssemblyRsvpPolicy.HeedRegardFloor)).Accepted.Should().BeTrue();

        // The policy's own comment: an enemy invited to parley has a LOWER bar than an ally, because coming to
        // talk does not require goodwill, only that they do not utterly hate the summoner. This pins that the
        // parley floor (not the ordinary heed floor) governs once InitiatorAtWarWithInvitee is true.
        [Test]
        public void GIVEN_an_enemy_parley_with_regard_between_the_two_floors_WHEN_deciding_THEN_the_lower_parley_bar_lets_them_come()
        {
            int between = (AssemblyRsvpPolicy.ParleyRegardFloor + AssemblyRsvpPolicy.HeedRegardFloor) / 2;

            Decide(Facts(parley: true, regard: between)).Accepted.Should().BeTrue();
        }

        // Even the lowered parley bar has a floor: an enemy who hates the summoner too much will not show up
        // even to talk.
        [Test]
        public void GIVEN_an_enemy_parley_with_regard_below_even_the_parley_floor_WHEN_deciding_THEN_it_declines_as_hostile()
            => Decide(Facts(parley: true, regard: AssemblyRsvpPolicy.ParleyRegardFloor - 1))
                .Reason.Should().Be(RsvpReason.DeclinedHostile);

        // Boundary companion to the test above: exactly at ParleyRegardFloor still accepts the parley.
        [Test]
        public void GIVEN_an_enemy_parley_with_regard_exactly_at_the_parley_floor_WHEN_deciding_THEN_it_accepts()
            => Decide(Facts(parley: true, regard: AssemblyRsvpPolicy.ParleyRegardFloor)).Accepted.Should().BeTrue();

        // ── Relevance / priority ────────────────────────────────────────────

        // The policy's own comment: the player cannot summon the whole map. A far host is only worth an
        // invitee's journey when the relationship justifies it; lukewarm regard alone is not enough.
        [Test]
        public void GIVEN_a_far_host_and_only_lukewarm_regard_WHEN_deciding_THEN_it_declines_as_not_worth_the_travel()
            => Decide(Facts(distance: AssemblyRsvpPolicy.FarDistanceTier, regard: AssemblyRsvpPolicy.WorthTravelRegard - 1))
                .Reason.Should().Be(RsvpReason.DeclinedNotWorthTravel);

        // Boundary companion: sitting exactly at WorthTravelRegard is enough to make the long journey worthwhile.
        [Test]
        public void GIVEN_a_far_host_but_genuine_regard_WHEN_deciding_THEN_it_accepts_the_journey()
            => Decide(Facts(distance: AssemblyRsvpPolicy.FarDistanceTier, regard: AssemblyRsvpPolicy.WorthTravelRegard))
                .Accepted.Should().BeTrue();

        // The relevance gate is scoped to FAR hosts only: one tier below FarDistanceTier, even neutral (0)
        // regard is not penalized, so nearby invitees are never wrongly filtered out for lukewarm regard.
        [Test]
        public void GIVEN_a_near_host_and_lukewarm_regard_WHEN_deciding_THEN_the_relevance_gate_does_not_apply()
            => Decide(Facts(distance: AssemblyRsvpPolicy.FarDistanceTier - 1, regard: 0)).Accepted.Should().BeTrue();

        // ── Happy path ──────────────────────────────────────────────────────

        // The baseline every other test overrides one axis of: nothing blocks the invitee, so the summons is
        // simply accepted.
        [Test]
        public void GIVEN_an_available_nearby_ally_with_decent_regard_WHEN_deciding_THEN_it_accepts()
        {
            RsvpOutcome o = Decide(Facts(regard: 25, distance: 1));

            o.Accepted.Should().BeTrue();
            o.Reason.Should().Be(RsvpReason.Accepted);
        }
    }
}
