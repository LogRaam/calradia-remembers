// Code written by Gabriel Mailhot, 01/08/2026.
// This policy decides the player-facing pacing dial for unprompted letters: how many are allowed to reach
// them per rolling window, and how often a lower-weight one is injected for diversity. It feeds the LetterBudget
// that LetterMailboxPolicy.SelectNext enforces. Modelled on InterceptionPacing: get the default wrong and every
// existing player's mailbox behaviour changes the moment this ships, even though nobody asked for anything to
// change; get a non-default setting wrong and the dial the player reached for does not actually do anything.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterPacingTests
    {
        // The load-bearing guarantee of any new setting: the default must be the status quo, exactly. A
        // player who never opens the menu must get precisely the numbers that shipped, or adding this dial
        // silently changes a game nobody asked to have changed.
        [Test]
        public void GIVEN_a_player_who_never_opens_the_menu_WHEN_the_mailbox_budget_is_read_THEN_it_matches_the_shipped_defaults_exactly()
        {
            LetterBudget budget = LetterPacingRules.BudgetFor(LetterPacing.Occasionally);

            budget.MaxPerWindow.Should().Be(LetterPacingRules.DefaultMaxPerWindow);
            budget.WindowDays.Should().Be(LetterPacingRules.DefaultWindowDays);
            budget.DiversityChancePercent.Should().Be(LetterPacingRules.DefaultDiversityChancePercent);
        }

        // A dial the player cannot feel is a lie. The number that actually governs how full the mailbox
        // feels is the RATE (letters allowed per day of window), not either raw number in isolation: a
        // longer window with a proportionally bigger cap could look "more generous" on paper while feeling
        // identical in play. Each step up must genuinely raise that rate.
        [Test]
        public void GIVEN_the_three_settings_WHEN_ordered_THEN_each_one_up_genuinely_means_a_fuller_mailbox()
        {
            double rarelyRate = RateOf(LetterPacing.Rarely);
            double occasionallyRate = RateOf(LetterPacing.Occasionally);
            double oftenRate = RateOf(LetterPacing.Often);

            rarelyRate.Should().BeLessThan(occasionallyRate);
            occasionallyRate.Should().BeLessThan(oftenRate);
        }

        // Diversity chance is its own dial: a fuller mailbox (Often) should also be the most willing to show
        // the player something other than the single loudest subject, since there is more competing for that
        // one slot. If this never moved with the other two, "diversity" would just be a fixed constant
        // wearing three different names.
        [Test]
        public void GIVEN_the_three_settings_WHEN_diversity_chance_is_read_THEN_it_rises_from_Rarely_to_Often()
        {
            LetterPacingRules.BudgetFor(LetterPacing.Rarely).DiversityChancePercent
                .Should().BeLessThan(LetterPacingRules.BudgetFor(LetterPacing.Occasionally).DiversityChancePercent);
            LetterPacingRules.BudgetFor(LetterPacing.Occasionally).DiversityChancePercent
                .Should().BeLessThan(LetterPacingRules.BudgetFor(LetterPacing.Often).DiversityChancePercent);
        }

        private static double RateOf(LetterPacing pacing)
        {
            LetterBudget budget = LetterPacingRules.BudgetFor(pacing);
            return (double)budget.MaxPerWindow / budget.WindowDays;
        }
    }
}
