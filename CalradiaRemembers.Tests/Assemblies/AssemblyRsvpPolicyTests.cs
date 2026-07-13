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

        [Test]
        public void GIVEN_a_companion_already_in_the_party_WHEN_deciding_THEN_they_are_accepted_even_if_far_or_cool()
        {
            RsvpOutcome o = Decide(Facts(companion: true, distance: 9, regard: -50, enemyGround: true));

            o.Accepted.Should().BeTrue();
            o.Reason.Should().Be(RsvpReason.Accepted);
        }

        // ── Availability ────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_dead_invitee_WHEN_deciding_THEN_it_declines_as_unavailable()
            => Decide(Facts(alive: false)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        [Test]
        public void GIVEN_an_imprisoned_invitee_WHEN_deciding_THEN_it_declines_as_unavailable()
            => Decide(Facts(prisoner: true)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        [Test]
        public void GIVEN_an_invitee_pinned_by_a_siege_or_battle_WHEN_deciding_THEN_it_declines_as_unavailable()
            => Decide(Facts(occupied: true)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        [Test]
        public void GIVEN_a_prisoner_on_enemy_ground_WHEN_deciding_THEN_availability_wins_over_the_ground_reason()
            => Decide(Facts(prisoner: true, enemyGround: true)).Reason.Should().Be(RsvpReason.DeclinedUnavailable);

        // ── Ground ──────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_host_owned_by_the_invitees_enemy_WHEN_deciding_THEN_it_declines_for_enemy_ground_even_when_liked()
            => Decide(Facts(enemyGround: true, regard: 80)).Reason.Should().Be(RsvpReason.DeclinedEnemyGround);

        [Test]
        public void GIVEN_enemy_ground_and_a_hostile_regard_WHEN_deciding_THEN_the_ground_reason_wins_over_hostility()
            => Decide(Facts(enemyGround: true, regard: -90)).Reason.Should().Be(RsvpReason.DeclinedEnemyGround);

        // ── Willingness ─────────────────────────────────────────────────────

        [Test]
        public void GIVEN_an_ally_whose_regard_is_below_the_heed_floor_WHEN_deciding_THEN_it_declines_as_hostile()
            => Decide(Facts(regard: AssemblyRsvpPolicy.HeedRegardFloor - 1)).Reason.Should().Be(RsvpReason.DeclinedHostile);

        [Test]
        public void GIVEN_an_ally_whose_regard_sits_exactly_at_the_heed_floor_WHEN_deciding_THEN_it_accepts()
            => Decide(Facts(regard: AssemblyRsvpPolicy.HeedRegardFloor)).Accepted.Should().BeTrue();

        [Test]
        public void GIVEN_an_enemy_parley_with_regard_between_the_two_floors_WHEN_deciding_THEN_the_lower_parley_bar_lets_them_come()
        {
            int between = (AssemblyRsvpPolicy.ParleyRegardFloor + AssemblyRsvpPolicy.HeedRegardFloor) / 2;

            Decide(Facts(parley: true, regard: between)).Accepted.Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_enemy_parley_with_regard_below_even_the_parley_floor_WHEN_deciding_THEN_it_declines_as_hostile()
            => Decide(Facts(parley: true, regard: AssemblyRsvpPolicy.ParleyRegardFloor - 1))
                .Reason.Should().Be(RsvpReason.DeclinedHostile);

        [Test]
        public void GIVEN_an_enemy_parley_with_regard_exactly_at_the_parley_floor_WHEN_deciding_THEN_it_accepts()
            => Decide(Facts(parley: true, regard: AssemblyRsvpPolicy.ParleyRegardFloor)).Accepted.Should().BeTrue();

        // ── Relevance / priority ────────────────────────────────────────────

        [Test]
        public void GIVEN_a_far_host_and_only_lukewarm_regard_WHEN_deciding_THEN_it_declines_as_not_worth_the_travel()
            => Decide(Facts(distance: AssemblyRsvpPolicy.FarDistanceTier, regard: AssemblyRsvpPolicy.WorthTravelRegard - 1))
                .Reason.Should().Be(RsvpReason.DeclinedNotWorthTravel);

        [Test]
        public void GIVEN_a_far_host_but_genuine_regard_WHEN_deciding_THEN_it_accepts_the_journey()
            => Decide(Facts(distance: AssemblyRsvpPolicy.FarDistanceTier, regard: AssemblyRsvpPolicy.WorthTravelRegard))
                .Accepted.Should().BeTrue();

        [Test]
        public void GIVEN_a_near_host_and_lukewarm_regard_WHEN_deciding_THEN_the_relevance_gate_does_not_apply()
            => Decide(Facts(distance: AssemblyRsvpPolicy.FarDistanceTier - 1, regard: 0)).Accepted.Should().BeTrue();

        // ── Happy path ──────────────────────────────────────────────────────

        [Test]
        public void GIVEN_an_available_nearby_ally_with_decent_regard_WHEN_deciding_THEN_it_accepts()
        {
            RsvpOutcome o = Decide(Facts(regard: 25, distance: 1));

            o.Accepted.Should().BeTrue();
            o.Reason.Should().Be(RsvpReason.Accepted);
        }
    }
}
