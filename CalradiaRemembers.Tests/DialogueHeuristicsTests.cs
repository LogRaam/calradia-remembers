// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the pure chat heuristics extracted from ChatViewModel: refusal detection, the "NPC asked a
// question" cue, witness-name role stripping, and response action lookup.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DialogueHeuristicsTests
    {
        // ── LooksLikeRefusal ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_an_out_of_character_refusal_WHEN_checked_THEN_it_is_detected_case_insensitively()
        {
            DialogueHeuristics.LooksLikeRefusal("I'm sorry, but As An AI I must decline.").Should().BeTrue();
            DialogueHeuristics.LooksLikeRefusal("This is against my guidelines.").Should().BeTrue();
        }

        [Test]
        public void GIVEN_ordinary_in_character_speech_WHEN_checked_THEN_it_is_not_a_refusal()
        {
            DialogueHeuristics.LooksLikeRefusal("You will tell me what I want to know, prisoner.").Should().BeFalse();
        }

        [Test]
        public void GIVEN_blank_text_WHEN_checked_THEN_it_is_not_a_refusal()
        {
            DialogueHeuristics.LooksLikeRefusal(null).Should().BeFalse();
            DialogueHeuristics.LooksLikeRefusal("   ").Should().BeFalse();
        }

        // ── NpcAskedQuestion ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_dialogue_ending_in_a_question_WHEN_checked_THEN_true()
        {
            DialogueHeuristics.NpcAskedQuestion("And what, exactly, do you intend to do about it?").Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_question_followed_by_a_stage_direction_WHEN_checked_THEN_still_true()
        {
            DialogueHeuristics.NpcAskedQuestion("Will you yield? *leans in close*").Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_statement_WHEN_checked_THEN_false()
        {
            DialogueHeuristics.NpcAskedQuestion("You will yield.").Should().BeFalse();
            DialogueHeuristics.NpcAskedQuestion(null).Should().BeFalse();
        }

        // ── StripWitnessRole ───────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_name_with_a_parenthetical_role_WHEN_stripped_THEN_only_the_name_remains()
        {
            DialogueHeuristics.StripWitnessRole("A soldier (one of your men, present on your orders)")
                .Should().Be("A soldier");
        }

        [Test]
        public void GIVEN_a_plain_name_WHEN_stripped_THEN_it_is_only_trimmed()
        {
            DialogueHeuristics.StripWitnessRole("  Derthert  ").Should().Be("Derthert");
        }

        // ── HasAction ──────────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_response_carrying_the_action_WHEN_checked_THEN_true_case_insensitively()
        {
            var response = new ParsedResponse { Dialogue = "", Actions = new[] { new GameAction { Type = "Escape_Attempt" } } };

            DialogueHeuristics.HasAction(response, "escape_attempt").Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_response_without_the_action_or_a_null_response_WHEN_checked_THEN_false()
        {
            var response = new ParsedResponse { Dialogue = "", Actions = new[] { new GameAction { Type = "change_relation" } } };

            DialogueHeuristics.HasAction(response, "escape_attempt").Should().BeFalse();
            DialogueHeuristics.HasAction(null, "escape_attempt").Should().BeFalse();
        }
    }
}
