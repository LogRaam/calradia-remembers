// Code written by Gabriel Mailhot, 03/11/2026.
// The reply budget and presence penalty a conversation is sent with. avalanche24 (Nexus, 03/11/2026): a local reasoning model
// was cut off mid-thought at the fixed 1200-token Compact budget, and the fixed 0.5 presence penalty cut its thinking short;
// Gabriel's "go": two menu settings. If this is wrong, Auto changes what every other player is sent, or a picked value
// leaves the range a model can work in.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ReplyShapePolicyTests
    {
        // STAKE: a player who never opens the menu is sent exactly what they were before (3000, or 1200 in Compact).
        [Test]
        public void GIVEN_the_budget_left_on_auto_WHEN_read_THEN_no_budget_is_chosen()
        {
            ReplyBudgetPolicy.Chosen(ReplyBudgetPolicy.Auto).Should().BeNull();
        }

        // STAKE: avalanche24's case: the budget the player picks is the one sent, Compact or not.
        [Test]
        public void GIVEN_a_budget_picked_WHEN_read_THEN_it_is_the_one_sent()
        {
            ReplyBudgetPolicy.Chosen(6000).Should().Be(6000);
        }

        // STAKE: a hand-edited config cannot send a budget too small for any reply, nor an absurd one.
        [Test]
        public void GIVEN_a_budget_out_of_range_WHEN_read_THEN_it_is_held_in_range()
        {
            ReplyBudgetPolicy.Chosen(10).Should().Be(ReplyBudgetPolicy.Min);
            ReplyBudgetPolicy.Chosen(999999).Should().Be(ReplyBudgetPolicy.Max);
        }

        // STAKE: the penalty everyone has been sent since July stays the default; 0 sends none, for a model it hurts.
        [Test]
        public void GIVEN_the_penalty_WHEN_read_THEN_the_default_stands_and_zero_is_allowed()
        {
            PresencePenaltyPolicy.Default.Should().Be(0.5f);
            PresencePenaltyPolicy.Clamp(0f).Should().Be(0f);
            PresencePenaltyPolicy.Clamp(5f).Should().Be(PresencePenaltyPolicy.Max);
            PresencePenaltyPolicy.Clamp(float.NaN).Should().Be(PresencePenaltyPolicy.Default);
        }
    }
}
