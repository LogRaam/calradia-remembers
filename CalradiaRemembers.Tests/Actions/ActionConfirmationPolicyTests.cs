// Code written by Gabriel Mailhot, 11/07/2026.
// Documents which LLM-proposed actions are gated behind an in-chat Y/n confirmation before they touch the
// player's gold or commitments, how a typed answer is read, and the exact wording shown. The LLM proposing a
// spend is not the player agreeing to it: per ActionConfirmationController's own header, this also tames the
// LLM re-emitting the same charge every turn (the reported repeated-charges bug). Feeds
// ActionConfirmationController.Preprocess (RequiresConfirmation decides Queued vs ExecuteInline), HandleInput
// (ParseAnswer routes Confirm/Decline/Recompose), and PromptNext (BuildQuestion is the literal line shown).
// Get RequiresConfirmation wrong and either the player's coin moves without being asked, or an NPC's own
// unrelated gift gets needlessly gated behind a prompt.

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
        // The only three action types where the PLAYER spends or commits (pays gold, hires into the party, gives
        // up a held prisoner): these alone must be queued for confirmation. Case-insensitive because the LLM's
        // action-type casing in its raw output is not guaranteed.
        [TestCase("take_gold")]
        [TestCase("join_party")]
        [TestCase("free_prisoner")]
        [TestCase("Take_Gold")]
        [TestCase("Free_Prisoner")]
        public void GIVEN_an_action_the_player_spends_or_commits_on_WHEN_checked_THEN_it_requires_confirmation(string actionType)
        {
            ActionConfirmationPolicy.RequiresConfirmation(actionType).Should().BeTrue();
        }

        // give_gold is the NPC's own initiative (a gift TO the player), never the player's, so gating it would
        // needlessly interrupt the player with a prompt for something they never agreed to spend.
        [Test]
        public void GIVEN_give_gold_the_npc_gives_the_player_coin_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation("give_gold").Should().BeFalse();
        }

        // Per the policy's own doc comment: consequences the player must not be able to veto (relation changes,
        // grudges, and so on) are never routed here, or the player could dodge a deserved relation penalty by
        // simply declining the prompt.
        [Test]
        public void GIVEN_an_action_type_that_does_not_move_coin_or_commit_service_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation("change_relation").Should().BeFalse();
        }

        // Defensive: a null action type must not throw or accidentally trip confirmation.
        [Test]
        public void GIVEN_a_null_action_type_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation(null).Should().BeFalse();
        }

        // Defensive: an empty action type must not throw or accidentally trip confirmation.
        [Test]
        public void GIVEN_an_empty_action_type_WHEN_checked_THEN_it_does_not_require_confirmation()
        {
            ActionConfirmationPolicy.RequiresConfirmation("").Should().BeFalse();
        }

        // Y is the explicit default (per ActionConfirmationController's header): just pressing Enter must
        // confirm, not silently do nothing, so a quick keystroke does not leave the player stuck mid-prompt.
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

        // A clear "n"/"no" must decline, so a player who does not want to pay is never charged.
        [TestCase("n")]
        [TestCase("no")]
        [TestCase("NO")]
        public void GIVEN_a_negative_answer_WHEN_parsed_THEN_it_is_a_decline(string input)
        {
            ActionConfirmationPolicy.ParseAnswer(input).Should().Be(ConfirmationAnswer.Decline);
        }

        // Free text that is neither yes nor no reads as the player moving on to a fresh message, not settling
        // the prompt: HandleInput declines every pending action on Recompose so nothing is left half-answered
        // and silently applied later.
        [TestCase("maybe")]
        [TestCase("tell me more")]
        [TestCase("gold?")]
        public void GIVEN_an_answer_that_is_neither_yes_nor_no_WHEN_parsed_THEN_it_is_a_recompose(string input)
        {
            ActionConfirmationPolicy.ParseAnswer(input).Should().Be(ConfirmationAnswer.Recompose);
        }

        // BuildQuestion covers all four gold/service action types for completeness of the vocabulary; give_gold
        // is exercised here even though RequiresConfirmation never actually gates it (it runs inline).
        [Test]
        public void GIVEN_give_gold_WHEN_building_the_question_THEN_it_asks_whether_the_player_accepts_the_coins()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("give_gold", "Lyfris the Knife", 500);

            question.Should().Be("Accept 500 coins from Lyfris the Knife?");
        }

        // The exact wording the player sees for a real gated spend: phrased as the player's OWN payment ("Give
        // ... coins?"), not the NPC's, so the prompt cannot be misread as a gift.
        [Test]
        public void GIVEN_take_gold_WHEN_building_the_question_THEN_it_asks_whether_the_player_pays_the_npc()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("take_gold", "Lyfris the Knife", 500);

            question.Should().Be("Give Lyfris the Knife 500 coins?");
        }

        // Names both the commitment (taking service) and its price, so the player confirms an informed hire,
        // not a blind "yes" to an unstated cost.
        [Test]
        public void GIVEN_join_party_WHEN_building_the_question_THEN_it_asks_whether_the_npc_takes_service()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("join_party", "Fercread the Pinder", 500);

            question.Should().Be("Let Fercread the Pinder take service in your party for 500 coins?");
        }

        // free_prisoner carries no coin amount at all (per BuildQuestion's own doc comment, amount is ignored
        // for this type): the question must read as the irreversible commitment it is, not reference a stray 0.
        [Test]
        public void GIVEN_free_prisoner_WHEN_building_the_question_THEN_it_asks_whether_to_set_the_prisoner_free_ignoring_amount()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("free_prisoner", "Garios", 0);

            question.Should().Be("Set Garios free?");
        }

        // Any action type the caller has not special-cased still needs SOME confirmation line rather than a
        // blank or crashing prompt, so an unrecognized type falls back to a generic question instead.
        [Test]
        public void GIVEN_an_unrecognized_action_type_WHEN_building_the_question_THEN_it_falls_back_to_a_generic_confirmation()
        {
            string question = ActionConfirmationPolicy.BuildQuestion("change_relation", "Fercread the Pinder", 500);

            question.Should().Be("Confirm this with Fercread the Pinder?");
        }
    }
}
