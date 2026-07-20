// Code written by Gabriel Mailhot, 20/07/2026.
// Documents the courier ration top-up boundary (courier robustness batch, ROADMAP 2026-07-20). The stake: a
// starved courier party is destroyed by the engine and its letter reported "lost on the road" with no visible
// cause; the top-up guarantees the pathological waits (the M5 landfall vigil) can never starve a courier.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CourierRationPolicyTests
    {
        // The refill fires only on an EMPTY larder: the spawn stock is the honest travel budget, and topping up
        // a merely-low courier would quietly turn every ride into a bottomless pantry.
        [Test]
        public void GIVEN_a_courier_with_any_food_left_WHEN_asked_THEN_no_top_up_is_granted()
        {
            CourierRationPolicy.ShouldTopUp(1).Should().BeFalse();
            CourierRationPolicy.ShouldTopUp(100).Should().BeFalse();
        }

        // An empty larder is refilled before the engine's starvation can destroy the party and mute the letter.
        [Test]
        public void GIVEN_a_courier_whose_larder_is_empty_WHEN_asked_THEN_the_top_up_is_granted()
        {
            CourierRationPolicy.ShouldTopUp(0).Should().BeTrue();
        }
    }
}
