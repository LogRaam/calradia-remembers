// Code written by Gabriel Mailhot, 31/10/2026.
// When a rider bound for the player is steered again, and how fast it rides. kurushimi00 (Nexus, 31/10/2026): riders "all
// requiring me to keep my party immobile to let them actually deliver the letters"; Gabriel, same day: "il faudrait qu'un
// cavalier courrier puisse rattrapper le joueur en mouvement". If this is wrong, a rider rides to where the player was and
// waits there, a faster player outruns every letter, or a far rider is re-ordered every frame for nothing.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Letters
{
    [TestFixture]
    public sealed class ReplyRiderPolicyTests
    {
        // STAKE: the report itself. A rider close behind a moving player must keep turning toward them, or it reaches the
        // spot the player has left and sits there while the player rides on.
        [Test]
        public void GIVEN_a_rider_close_behind_a_moving_player_WHEN_the_player_has_moved_on_THEN_the_rider_is_steered_again()
        {
            ReplyRiderPolicy.ShouldRefreshTarget(new RiderChaseFacts {DistanceToPlayer = 3f, TargetDrift = 0.6f}).Should().BeTrue();
        }

        // STAKE: the player standing still, or barely moving, costs no new order: every rider in flight is read on every
        // campaign tick, and a kurushimi00 flood is nine of them.
        [Test]
        public void GIVEN_a_player_who_has_barely_moved_WHEN_read_THEN_the_rider_keeps_its_order()
        {
            ReplyRiderPolicy.ShouldRefreshTarget(new RiderChaseFacts {DistanceToPlayer = 3f, TargetDrift = 0.1f}).Should().BeFalse();
        }

        // STAKE: a rider a hundred units out loses nothing by aiming a few units wide, and turns only when the error starts
        // to count; it does turn then, or it would ride a long way to a point the player left days ago.
        [Test]
        public void GIVEN_a_far_rider_WHEN_the_player_drifts_THEN_only_a_drift_worth_its_distance_turns_it()
        {
            ReplyRiderPolicy.ShouldRefreshTarget(new RiderChaseFacts {DistanceToPlayer = 100f, TargetDrift = 5f}).Should().BeFalse();
            ReplyRiderPolicy.ShouldRefreshTarget(new RiderChaseFacts {DistanceToPlayer = 100f, TargetDrift = 11f}).Should().BeTrue();
        }

        // STAKE: an ordinary party (about 5 an hour) is caught at the couriers' own speed; nothing in the map's pace changes
        // for a player who rides at an ordinary speed.
        [Test]
        public void GIVEN_a_player_slower_than_the_couriers_WHEN_the_pace_is_read_THEN_the_rider_keeps_the_couriers_own_speed()
        {
            ReplyRiderPolicy.PaceFor(5f, 6.5f).Should().Be(6.5f);
        }

        // STAKE: a mounted party with the right perks passes 6.5; a rider that could never close the gap would ride behind
        // that player until its letter gave up on the road (Gabriel: a rider must be able to catch a moving player).
        [Test]
        public void GIVEN_a_player_faster_than_the_couriers_WHEN_the_pace_is_read_THEN_the_rider_still_closes_the_gap()
        {
            ReplyRiderPolicy.PaceFor(9f, 6.5f).Should().BeGreaterThan(9f);
        }
    }
}
