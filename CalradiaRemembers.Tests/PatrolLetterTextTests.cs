// Code written by Gabriel Mailhot, 17/10/2026.
// The patrolling companion's monthly letter and the player's stop order by letter (Gabriel, 16/10/2026). Wrong, the
// companion reports a fight that never happened, a "come back" goes unheard, a "keep on" brings the men home, or the
// control line shows up in the letter the player reads.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PatrolLetterTextTests
    {
        // STAKE: the letter tells the patrol's real record, the place, the days, the fights and the men, so the model
        // has nothing to invent.
        [Test]
        public void GIVEN_a_month_of_patrol_WHEN_the_letter_context_is_built_THEN_it_holds_the_real_record()
        {
            PatrolLetterText.Context(new PatrolReportFacts {SettlementName = "Ebereth", DaysOut = 30, MenAtDeparture = 30, MenFitNow = 27, BattlesWon = 2, BattlesLost = 1, FoesBeaten = 48})
                            .Should().Be("You have patrolled around Ebereth for 30 days, with the 30 men the player gave you. You have won 2 fights, against 48 men in all, and lost 1. 27 of your men are fit now.");
        }

        // STAKE: a quiet month says so, rather than leaving the model to fill the silence with a fight.
        [Test]
        public void GIVEN_a_month_without_a_fight_WHEN_the_letter_context_is_built_THEN_it_says_no_fight_came()
        {
            PatrolLetterText.Context(new PatrolReportFacts {SettlementName = "Ebereth", DaysOut = 30, MenAtDeparture = 20, MenFitNow = 20})
                            .Should().Contain("No fight has come your way.");
        }

        // STAKE: Gabriel: the player stops the patrol by letter; the order is read wherever the line sits.
        [Test]
        public void GIVEN_an_answer_with_the_stop_line_WHEN_read_THEN_the_patrol_is_ordered_home()
        {
            PatrolLetterText.OrdersStop("DELAY: 2\n\nI will come back at once.\n[LETTER_TONE] warmer\n[PATROL] stop").Should().BeTrue();
        }

        // STAKE: no line, or a line that is not a stop, means the patrol goes on (Gabriel: no answer, no change).
        [Test]
        public void GIVEN_an_answer_without_a_stop_WHEN_read_THEN_the_patrol_goes_on()
        {
            PatrolLetterText.OrdersStop("DELAY: 2\n\nI will keep watch.\n[LETTER_TONE] same").Should().BeFalse();
            PatrolLetterText.OrdersStop(null).Should().BeFalse();
        }

        // STAKE: the control line is metadata; it never reaches the letter the player reads.
        [Test]
        public void GIVEN_an_answer_with_the_stop_line_WHEN_the_letter_is_kept_THEN_the_line_is_gone()
        {
            PatrolLetterText.StripOrderLine("I will come back at once.\n[PATROL] stop").Should().Be("I will come back at once.");
        }
    }
}
