// Code written by Gabriel Mailhot, 04/08/2026.
// Documents give_gold's pure policy: the payable-amount clamp mirroring the existing 1:1 give_gold action's own
// solvency rule (BannerlordGameStateBridge.ExecuteGiveGold). The stake across every test here is the same: a
// council pledge must never conjure coin the giver does not hold, and a pledge from an insolvent giver must be
// refused rather than silently applied for zero denars while still reporting success.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class GoldPledgePolicyTests
    {
        // The ordinary case: a modest pledge well within both the giver's purse and the ceiling passes through
        // unchanged, exactly as the 1:1 give_gold bridge already behaves for an affordable amount.
        [Test]
        public void GIVEN_a_proposed_amount_the_giver_can_afford_WHEN_clamped_THEN_it_is_unchanged()
        {
            GoldPledgePolicy.ClampPayable(200, 5000).Should().Be(200);
        }

        // The giver's own purse is the first ceiling: a pledge must never exceed what they actually hold, or the
        // lift would conjure coin the giver never had, the exact class of bug the 1:1 bridge's own clamp guards.
        [Test]
        public void GIVEN_a_proposed_amount_above_the_givers_gold_WHEN_clamped_THEN_it_is_capped_to_their_gold()
        {
            GoldPledgePolicy.ClampPayable(500, 300).Should().Be(300);
        }

        // The second ceiling, the same fixed cap the 1:1 give_gold bridge already applies: a giver flush with
        // gold still cannot pledge an unbounded sum in one council pledge.
        [Test]
        public void GIVEN_a_proposed_amount_above_the_ceiling_WHEN_clamped_THEN_it_is_capped_to_the_maximum()
        {
            GoldPledgePolicy.ClampPayable(50000, 100000).Should().Be(GoldPledgePolicy.MaxPayable);
        }

        // A giver holding no gold at all must yield a zero payable amount: this is the lift's own signal to
        // refuse the pledge outright ("they hold no gold to give"), never to apply it for nothing while still
        // reporting the pledge as kept.
        [TestCase(0)]
        [TestCase(-50)]
        public void GIVEN_a_giver_with_no_gold_WHEN_clamped_THEN_the_payable_amount_is_zero(int giverGold)
        {
            GoldPledgePolicy.ClampPayable(200, giverGold).Should().Be(0);
        }

        // A non-positive proposal (a malformed or unparsed pledge) must never become a positive payable amount,
        // whatever the giver's own gold: there is nothing real to pay out.
        [TestCase(0)]
        [TestCase(-100)]
        public void GIVEN_a_non_positive_proposed_amount_WHEN_clamped_THEN_the_payable_amount_is_zero(int proposed)
        {
            GoldPledgePolicy.ClampPayable(proposed, 5000).Should().Be(0);
        }

        // Boundary: a proposal exactly equal to both the giver's gold and/or the ceiling passes through
        // unchanged, never nudged down by one denar past what is actually affordable.
        [Test]
        public void GIVEN_a_proposed_amount_exactly_at_a_boundary_WHEN_clamped_THEN_it_is_unchanged()
        {
            GoldPledgePolicy.ClampPayable(300, 300).Should().Be(300);
            GoldPledgePolicy.ClampPayable(GoldPledgePolicy.MaxPayable, GoldPledgePolicy.MaxPayable).Should().Be(GoldPledgePolicy.MaxPayable);
        }
    }
}
