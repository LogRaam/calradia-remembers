// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the pure chat heuristics extracted from ChatViewModel: refusal detection, the "NPC asked a
// question" cue, witness-name role stripping, and response action lookup.
//
// WHY IT MATTERS: LooksLikeRefusal is the guard that keeps a model safety-guardrail reply ("I must decline...")
// from ever being shown to the player under the NPC's name; ChatViewModel.Exchanges.cs checks it on every
// dialogue AND narration before rendering, and on a hit concludes the scene in character instead, so a
// guardrail message never breaks immersion. HasAction backs the captive-scene escape/release resolution (the
// mod, not the LLM, owns the outcome). StripWitnessRole keeps a witness reaction from displaying the model's
// full prompt-label text (e.g. its role parenthetical) to the player. NpcAskedQuestion documents the
// end-of-turn cue but is not yet wired to a call site as of this writing.

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

        // Miss this and the whole point of the guard is lost: the "As An AI I must decline" text would be
        // rendered as the character's own words, which is precisely the immersion break this heuristic exists
        // to prevent. Two differently-worded markers, case-insensitive, pin the detection isn't accidentally
        // literal-cased or single-marker-only.
        [Test]
        public void GIVEN_an_out_of_character_refusal_WHEN_checked_THEN_it_is_detected_case_insensitively()
        {
            DialogueHeuristics.LooksLikeRefusal("I'm sorry, but As An AI I must decline.").Should().BeTrue();
            DialogueHeuristics.LooksLikeRefusal("This is against my guidelines.").Should().BeTrue();
        }

        // The other side of the risk: an over-eager marker list would falsely flag ordinary hostile
        // in-character speech as a refusal and cut the scene short for no reason.
        [Test]
        public void GIVEN_ordinary_in_character_speech_WHEN_checked_THEN_it_is_not_a_refusal()
        {
            DialogueHeuristics.LooksLikeRefusal("You will tell me what I want to know, prisoner.").Should().BeFalse();
        }

        // Gabriel (04/10/2026), the werewolf tale's scene in Enhanced mode: this refusal, word for word, was shown
        // under the narrator's name, because no marker matched it ("non-consensual" was listed only with its
        // hyphen). Missed, it is never hidden and never sent on to the NSFW model.
        [Test]
        public void GIVEN_the_safety_refusal_a_scene_narrator_wrote_in_play_WHEN_checked_THEN_it_is_detected()
        {
            DialogueHeuristics.LooksLikeRefusal("I cannot fulfill this request. My purpose is to provide safe and positive interactions, and generating "
                                              + "content that depicts sexual violence or nonconsensual sexual content is a violation of my safety policies.")
                              .Should().BeTrue();
            DialogueHeuristics.LooksLikeRefusal("I can't help with that request.").Should().BeTrue();
            DialogueHeuristics.LooksLikeRefusal("Sorry, I'm not able to generate that. It goes against the content policy.").Should().BeTrue();
        }

        // The new markers speak of the request and the policy, never of a deed: a narrator or a lord saying they
        // cannot do something in the story is not a refusal.
        [Test]
        public void GIVEN_a_character_or_narrator_who_cannot_do_something_in_the_story_WHEN_checked_THEN_it_is_not_a_refusal()
        {
            DialogueHeuristics.LooksLikeRefusal("He coughs hard. He cannot stand.").Should().BeFalse();
            DialogueHeuristics.LooksLikeRefusal("I can't help with that, stranger. Ask the miller.").Should().BeFalse();
            DialogueHeuristics.LooksLikeRefusal("I cannot let you pass, by order of the count.").Should().BeFalse();
        }

        // Fail closed on missing text: an empty or null dialogue line must not itself trip the refusal path,
        // which is handled separately (as a genuinely empty beat), not folded into this heuristic.
        [Test]
        public void GIVEN_blank_text_WHEN_checked_THEN_it_is_not_a_refusal()
        {
            DialogueHeuristics.LooksLikeRefusal(null).Should().BeFalse();
            DialogueHeuristics.LooksLikeRefusal("   ").Should().BeFalse();
        }

        // ── NpcAskedQuestion ───────────────────────────────────────────────────

        // Per the XML doc, this cue signals the other party may now respond; pinning the plain case first.
        [Test]
        public void GIVEN_dialogue_ending_in_a_question_WHEN_checked_THEN_true()
        {
            DialogueHeuristics.NpcAskedQuestion("And what, exactly, do you intend to do about it?").Should().BeTrue();
        }

        // A trailing stage direction (*leans in close*) must not mask the question mark before it; the regex
        // strips it before checking, so an in-character gesture can never silently swallow the cue.
        [Test]
        public void GIVEN_a_question_followed_by_a_stage_direction_WHEN_checked_THEN_still_true()
        {
            DialogueHeuristics.NpcAskedQuestion("Will you yield? *leans in close*").Should().BeTrue();
        }

        // The negative and null cases, so a plain statement or a missing line reads as "no question asked"
        // rather than defaulting true.
        [Test]
        public void GIVEN_a_statement_WHEN_checked_THEN_false()
        {
            DialogueHeuristics.NpcAskedQuestion("You will yield.").Should().BeFalse();
            DialogueHeuristics.NpcAskedQuestion(null).Should().BeFalse();
        }

        // ── StripWitnessRole ───────────────────────────────────────────────────

        // The reported bug: the LLM sometimes echoes the whole prompt label ("A soldier (one of your men,
        // present on your orders)") into the witness reaction's name field. Left unstripped, that role text
        // would both fail to match the real witness for routing AND leak to the player's screen verbatim.
        [Test]
        public void GIVEN_a_name_with_a_parenthetical_role_WHEN_stripped_THEN_only_the_name_remains()
        {
            DialogueHeuristics.StripWitnessRole("A soldier (one of your men, present on your orders)")
                .Should().Be("A soldier");
        }

        // The common case (a named hero, no parenthetical) must pass through untouched but trimmed, so the
        // stripping logic never mangles an ordinary witness name.
        [Test]
        public void GIVEN_a_plain_name_WHEN_stripped_THEN_it_is_only_trimmed()
        {
            DialogueHeuristics.StripWitnessRole("  Derthert  ").Should().Be("Derthert");
        }

        // ── HasAction ──────────────────────────────────────────────────────────

        // Backs the captive-scene escape/release resolution: the mod, not the LLM, decides the outcome, but it
        // first has to reliably detect that the model proposed one. Case-insensitivity matters because the
        // LLM's own casing of an action type is not guaranteed.
        [Test]
        public void GIVEN_a_response_carrying_the_action_WHEN_checked_THEN_true_case_insensitively()
        {
            var response = new ParsedResponse { Dialogue = "", Actions = new[] { new GameAction { Type = "Escape_Attempt" } } };

            DialogueHeuristics.HasAction(response, "escape_attempt").Should().BeTrue();
        }

        // Fail safe on absence: a response carrying an unrelated action, or no response at all, must never be
        // read as authorizing an escape or release the model never actually proposed.
        [Test]
        public void GIVEN_a_response_without_the_action_or_a_null_response_WHEN_checked_THEN_false()
        {
            var response = new ParsedResponse { Dialogue = "", Actions = new[] { new GameAction { Type = "change_relation" } } };

            DialogueHeuristics.HasAction(response, "escape_attempt").Should().BeFalse();
            DialogueHeuristics.HasAction(null, "escape_attempt").Should().BeFalse();
        }
    }
}
