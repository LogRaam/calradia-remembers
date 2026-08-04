// Code written by Gabriel Mailhot, 03/08/2026.
// Protects the FIRST governance executor's own refusal rule (CouncilLift.SettleAppointGovernor). Author's
// condition: appointing a companion or clan lord as governor is a real pledge the lift may carry out, and the
// prospective governor MAY refuse it - but only MECHANICALLY, from their own standing toward the player, never
// as a narrated refusal invented after ChangeGovernorAction already ran. That is exactly the phantom-deed class
// this project eliminates elsewhere (a quest "completed" with no verified deed, a relation gain with no gate):
// here the failure mode would be a governor appointed in the save data while the story tells the player they
// were turned down, or the reverse. Getting the boundary wrong in either direction reopens that class of bug.
//
// The standing floor (20) deliberately mirrors StanceGatePolicy.DelegatedRiskTrustFloor, the codebase's existing
// threshold for "the NPC risks exposing themselves on the player's word" (used for lending troops/influence):
// governing a fief is the same kind of exposure, so this reuses that established number rather than inventing
// a second one for the same class of trust.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class GovernorAppointmentPolicyTests
    {
        // The ordinary accept case: standing well above the floor must accept, or a trusted, long-proven
        // companion could still be turned away at the lift for no discoverable reason.
        [Test]
        public void GIVEN_standing_well_above_the_floor_WHEN_deciding_THEN_the_post_is_accepted()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(standingTowardPlayer: 50);

            verdict.Accepted.Should().BeTrue();
            verdict.RefusalReason.Should().BeNull();
        }

        // The ordinary refuse case: standing well below the floor must refuse, or the mechanical-refusal
        // condition would be dead code, and every appointment would silently succeed regardless of trust.
        [Test]
        public void GIVEN_standing_well_below_the_floor_WHEN_deciding_THEN_the_post_is_refused()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(standingTowardPlayer: 0);

            verdict.Accepted.Should().BeFalse();
            verdict.RefusalReason.Should().NotBeNullOrWhiteSpace();
        }

        // StandingFloor is inclusive (>=): pins that standing sitting EXACTLY on the tuning value already
        // accepts, so a future retune of the constant can't silently flip this boundary case to a refusal.
        [Test]
        public void GIVEN_standing_exactly_at_the_floor_WHEN_deciding_THEN_the_post_is_accepted()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(GovernorAppointmentPolicy.StandingFloor);

            verdict.Accepted.Should().BeTrue();
        }

        // The far side of the same boundary: one point under the floor must refuse, confirming the policy
        // compares by ordering (>=) rather than by equality with the floor alone.
        [Test]
        public void GIVEN_standing_one_point_below_the_floor_WHEN_deciding_THEN_the_post_is_refused()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(GovernorAppointmentPolicy.StandingFloor - 1);

            verdict.Accepted.Should().BeFalse();
        }

        // A negative standing (an NPC who actively dislikes the player) must refuse just as plainly as a
        // neutral one: this is the class of NPC most likely to be tested first in-game, so it must not
        // accidentally fall through to acceptance via an unguarded arithmetic edge case.
        [Test]
        public void GIVEN_negative_standing_WHEN_deciding_THEN_the_post_is_refused()
        {
            GovernorAppointmentVerdict verdict = GovernorAppointmentPolicy.Decide(standingTowardPlayer: -30);

            verdict.Accepted.Should().BeFalse();
        }
    }
}
