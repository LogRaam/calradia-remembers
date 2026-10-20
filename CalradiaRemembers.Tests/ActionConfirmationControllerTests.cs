// Code written by Gabriel Mailhot, 11/07/2026.
// Covers the decision logic the in-chat action-confirmation controller composes. ActionConfirmationController
// itself lives in the net472 mod project (engine-bound: Hero, NpcMemoryCampaignBehavior, StoryLog, ModLog, the
// ExecuteAction bridge), which this net10 test project cannot reference, so the controller's queue/apply flow is
// exercised in game rather than here (the same constraint the round-table extraction hit, see
// RoundTableControllerTests). What is pinned below is the pure ActionConfirmationPolicy the controller drives its
// every branch through: which action types get queued vs run inline (Preprocess), how a typed answer routes
// (HandleInput), and the exact prompt text (PromptNext / the Y/n line). Behaviour must stay identical after the
// extraction, so these guard the seam the controller now depends on.

#region

using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ActionConfirmationControllerTests
    {
        // Preprocess: a player-outgoing spend is queued for confirmation (ActionDisposition.Queued in the mod),
        // which the controller decides purely by ActionConfirmationPolicy.RequiresConfirmation.
        [TestCase("take_gold")]
        [TestCase("join_party")]
        public void GIVEN_a_player_outgoing_spend_WHEN_preprocessed_THEN_it_is_gated_for_confirmation(string actionType)
        {
            ActionConfirmationPolicy.RequiresConfirmation(actionType).Should().BeTrue();
        }

        // Preprocess: give_gold (the NPC's own gift to the player) and non-coin consequences run inline, never gated.
        [TestCase("give_gold")]
        [TestCase("change_relation")]
        public void GIVEN_an_npc_gift_or_a_non_coin_consequence_WHEN_preprocessed_THEN_it_runs_inline(string actionType)
        {
            ActionConfirmationPolicy.RequiresConfirmation(actionType).Should().BeFalse();
        }

        // HandleInput: Enter (blank) and y/yes confirm and apply the pending action.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("y")]
        [TestCase("yes")]
        public void GIVEN_an_empty_or_affirmative_answer_WHEN_handled_THEN_the_action_is_confirmed(string raw)
        {
            ActionConfirmationPolicy.ParseAnswer(raw).Should().Be(ConfirmationAnswer.Confirm);
        }

        // HandleInput: n/no declines the pending action (dropped, correction fed back through the host).
        [TestCase("n")]
        [TestCase("no")]
        public void GIVEN_a_negative_answer_WHEN_handled_THEN_the_action_is_declined(string raw)
        {
            ActionConfirmationPolicy.ParseAnswer(raw).Should().Be(ConfirmationAnswer.Decline);
        }

        // HandleInput: anything else reads as recompose, which the controller routes to host.SendPlayerMessage
        // after declining every pending action.
        [TestCase("tell me more")]
        [TestCase("actually, keep your coin")]
        public void GIVEN_a_free_text_answer_WHEN_handled_THEN_it_recomposes_into_a_fresh_turn(string raw)
        {
            ActionConfirmationPolicy.ParseAnswer(raw).Should().Be(ConfirmationAnswer.Recompose);
        }

        // PromptNext: the OOC line the controller shows for a queued take_gold is the policy's exact question.
        [Test]
        public void GIVEN_a_queued_take_gold_WHEN_prompting_THEN_the_question_asks_the_player_to_pay()
        {
            ActionConfirmationPolicy.BuildQuestion("take_gold", "Lyfris the Knife", 500)
                .Should().Be("Give Lyfris the Knife 500 coins?");
        }

        // PromptNext: the OOC line for a queued join_party names the service and its price.
        [Test]
        public void GIVEN_a_queued_join_party_WHEN_prompting_THEN_the_question_asks_about_taking_service()
        {
            ActionConfirmationPolicy.BuildQuestion("join_party", "Fercread the Pinder", 500)
                .Should().Be("Let Fercread the Pinder take service in your party for 500 coins?");
        }

        // STAKE: tashmetu (20/10/2026): a groom's father asks the player for his daughter's hand. The player's yes is asked in
        // the chat, naming her and the groom, so she is never wed on the model's reading of a sentence.
        [Test]
        public void GIVEN_a_hand_asked_of_the_player_WHEN_prompting_THEN_the_question_names_her_and_the_groom()
        {
            ActionConfirmationPolicy.BuildHandQuestion(new HandConfirmationFacts {BrideName = "Ylva", SuitorName = "Erek"})
                .Should().Be("Give Ylva's hand to Erek?");
        }
    }
}
