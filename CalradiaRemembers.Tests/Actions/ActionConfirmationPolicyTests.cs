// Code written by Gabriel Mailhot, 11/07/2026.

#region

using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
    [TestFixture]
    public sealed class ActionConfirmationPolicyTests
    {
        [TestCase("take_gold")]
        [TestCase("join_party")]
        [TestCase("free_prisoner")]
        [TestCase("Take_Gold")]
        [TestCase("Free_Prisoner")]
        public void GIVEN_an_action_the_player_spends_or_commits_on_WHEN_checked_THEN_it_requires_confirmation(string actionType)
        {
            ActionConfirmationPolicy.RequiresConfirmation(actionType).Should().BeTrue();
        }

        [Test]
        public void GIVEN_give_gold_the_npc_gives_the_player_coin_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation("give_gold").Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_action_type_that_does_not_move_coin_or_commit_service_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation("change_relation").Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_null_action_type_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation(null).Should().BeFalse();
        }

        [Test]
        public void GIVEN_an_empty_action_type_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation("").Should().BeFalse();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("y")]
        [TestCase("Y")]
        [TestCase("yes")]
        [TestCase("YES")]
        [TestCase("  y  ")]
        public void GIVEN_an_empty_or_affirmative_answer_WHEN_parsed_THEN_it_is_a_confirm(string input)
        {
            ActionConfirmationPolicy.ParseAnswer(input).Should().Be(ConfirmationAnswer.Confirm);
        }

        [TestCase("n")]
        [TestCase("no")]
        [TestCase("NO")]
        public void GIVEN_a_negative_answer_WHEN_parsed_THEN_it_is_a_decline(string input)
        {
            ActionConfirmationPolicy.ParseAnswer(input).Should().Be(ConfirmationAnswer.Decline);
        }

        [TestCase("maybe")]
        [TestCase("tell me more")]
        [TestCase("gold?")]
        public void GIVEN_an_answer_that_is_neither_yes_nor_no_WHEN_parsed_THEN_it_is_a_recompose(string input)
        {
            ActionConfirmationPolicy.ParseAnswer(input).Should().Be(ConfirmationAnswer.Recompose);
        }

        [Test]
        public void GIVEN_give_gold_WHEN_building_the_question_THEN_it_asks_whether_the_player_accepts_the_coins()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("give_gold", "Lyfris the Knife", 500);

            question.Should().Be("Accept 500 coins from Lyfris the Knife?");
        }

        [Test]
        public void GIVEN_take_gold_WHEN_building_the_question_THEN_it_asks_whether_the_player_pays_the_npc()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("take_gold", "Lyfris the Knife", 500);

            question.Should().Be("Give Lyfris the Knife 500 coins?");
        }

        [Test]
        public void GIVEN_join_party_WHEN_building_the_question_THEN_it_asks_whether_the_npc_takes_service()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("join_party", "Fercread the Pinder", 500);

            question.Should().Be("Let Fercread the Pinder take service in your party for 500 coins?");
        }

        [Test]
        public void GIVEN_free_prisoner_WHEN_building_the_question_THEN_it_asks_whether_to_set_the_prisoner_free_ignoring_amount()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("free_prisoner", "Garios", 0);

            question.Should().Be("Set Garios free?");
        }

        [Test]
        public void GIVEN_an_unrecognized_action_type_WHEN_building_the_question_THEN_it_falls_back_to_a_generic_confirmation()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("change_relation", "Fercread the Pinder", 500);

            question.Should().Be("Confirm this with Fercread the Pinder?");
        }
    }
}
