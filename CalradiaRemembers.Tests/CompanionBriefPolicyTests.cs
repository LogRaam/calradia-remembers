// Code written by Gabriel Mailhot, 07/09/2026.
// Protects the companion-briefing pillar's pure core: how a private word given to a companion before "A word
// with you..." continues stacks, bounds, and expires. Get Stack wrong and a second briefing either wipes the
// first (violating Gabriel's ruling that briefings stack) or grows the companion's slice of the prompt without
// limit (eating the always-on budget every turn of the conversation it rides into). Get HeroesToExpireOnClose
// wrong and a brief either leaks into an unrelated later conversation, or is wiped the instant it is given
// (before it ever reaches the conversation it was actually meant for).

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionBriefPolicyTests
   {
      // ── Stack: first briefing ────────────────────────────────────────────────

      // No prior brief: the first instruction simply becomes the whole brief, trimmed of stray whitespace so a
      // player's accidental leading/trailing spaces never leak into the prompt.
      [Test]
      public void GIVEN_no_existing_brief_WHEN_stacking_the_first_instruction_THEN_it_becomes_the_whole_brief()
      {
         string result = CompanionBriefPolicy.Stack(null, "  Press him on the grain tax.  ");

         result.Should().Be("Press him on the grain tax.");
      }

      // ── Stack: ruling #3, several briefings STACK rather than replace ───────

      // Gabriel's ruling: a player may go back having forgotten something, so a second briefing must not erase
      // the first, both must survive, in the order they were given, so the companion carries the whole
      // standing instruction into the conversation that follows.
      [Test]
      public void GIVEN_an_existing_brief_WHEN_stacking_a_second_instruction_THEN_both_survive_in_order()
      {
         string first = CompanionBriefPolicy.Stack(null, "Be gentle with him.");
         string second = CompanionBriefPolicy.Stack(first, "Press him on the grain tax.");

         second.Should().Contain("Be gentle with him.").And.Contain("Press him on the grain tax.");
         second.IndexOf("Be gentle", System.StringComparison.Ordinal)
            .Should().BeLessThan(second.IndexOf("Press him", System.StringComparison.Ordinal));
      }

      // A blank send (an accidental empty message somehow reaching this seam) must never wipe or pad a standing
      // brief: the existing text survives untouched.
      [Test]
      public void GIVEN_an_existing_brief_WHEN_stacking_a_blank_instruction_THEN_the_existing_brief_is_unchanged()
      {
         string result = CompanionBriefPolicy.Stack("Be gentle with him.", "   ");

         result.Should().Be("Be gentle with him.");
      }

      // No prior brief and a blank instruction: there is nothing to carry, so the result is null (never an
      // empty string masquerading as "a brief exists").
      [Test]
      public void GIVEN_no_existing_brief_WHEN_stacking_a_blank_instruction_THEN_the_result_is_null()
      {
         CompanionBriefPolicy.Stack(null, "").Should().BeNull();
      }

      // ── Stack: bounded total, drop-oldest ────────────────────────────────────

      // The lead's constraint on ruling #3: an unbounded stack would eat the prompt budget every turn of the
      // conversation it rides into (this is an always-on section, not a one-shot cue). The result must never
      // grow past the cap.
      [Test]
      public void GIVEN_stacked_instructions_exceeding_the_cap_WHEN_stacking_THEN_the_result_never_exceeds_the_cap()
      {
         string brief = "";
         for (var i = 0; i < 20; i++)
            brief = CompanionBriefPolicy.Stack(brief, $"Instruction number {i}, remember to ask about the harvest and the garrison.");

         brief.Length.Should().BeLessThanOrEqualTo(CompanionBriefPolicy.MaxChars);
      }

      // REVERSED 2026-09-13, and the reversal is the whole point. The rule used to be "drop the oldest, the
      // most recent instruction is the one the player means". That is true when he comes BACK to add
      // something, and false inside a single briefing, where the FIRST sentence is the order and everything
      // after it is reward, reassurance and "did you accept?".
      //
      // Gabriel's own briefing, measured from his log: four messages, 1,000 characters, bounded to the last
      // 500. What survived was the promise of protection and "we go now". What was cut was "I need you to try
      // to charm her. If you can become her lover, we'll have a good influence in the Empire." His companion
      // walked into the meeting with the negotiation and none of the mission, and played it exactly like a man
      // who had been told nothing - because he had.
      //
      // So the head and the tail both survive and the MIDDLE goes: the order, and the latest word on it.
      [Test]
      public void GIVEN_the_stack_overflows_the_cap_WHEN_stacking_THEN_the_order_survives_with_the_latest_word()
      {
         string brief = "";
         for (var i = 0; i < 60; i++)
            brief = CompanionBriefPolicy.Stack(brief, $"Instruction number {i}, remember to ask about the harvest and the garrison.");

         brief.Length.Should().BeLessThanOrEqualTo(CompanionBriefPolicy.MaxChars);
         brief.Should().Contain("Instruction number 0,", "the first thing said is the mission");
         brief.Should().Contain("Instruction number 59,", "the last thing said is the latest amendment");
         brief.Should().NotContain("Instruction number 30,", "the middle is what a briefing can afford to lose");
      }

      // And the seam must be visible, so nobody - model or reader - runs the two halves together into a
      // sentence neither of them said.
      [Test]
      public void GIVEN_a_brief_cut_in_the_middle_WHEN_read_THEN_the_gap_is_marked()
      {
         string brief = "";
         for (var i = 0; i < 60; i++)
            brief = CompanionBriefPolicy.Stack(brief, $"Instruction number {i}, remember to ask about the harvest and the garrison.");

         brief.Should().Contain("...");
      }

      // A single instruction alone exceeding the cap (an extreme edge case) must still be bounded, not left to
      // blow the budget just because there was no prior text to trim from.
      [Test]
      public void GIVEN_a_single_instruction_longer_than_the_cap_WHEN_stacking_THEN_it_is_still_bounded()
      {
         string longInstruction = new string('a', CompanionBriefPolicy.MaxChars + 200);

         string result = CompanionBriefPolicy.Stack(null, longInstruction);

         result.Length.Should().BeLessThanOrEqualTo(CompanionBriefPolicy.MaxChars);
      }

      // The trim must land on a word boundary, not slice a word in half: a fragment like "arvest" instead of
      // "harvest" would read as corrupted text to the model, not a deliberately-trimmed older instruction.
      [Test]
      public void GIVEN_the_cap_is_exceeded_WHEN_trimming_THEN_the_cut_lands_on_a_word_boundary()
      {
         string brief = "";
         for (var i = 0; i < 20; i++)
            brief = CompanionBriefPolicy.Stack(brief, $"Instruction number {i}, remember to ask about the harvest and the garrison.");

         brief.Should().NotStartWith(" ");
         char firstChar = brief[0];
         char.IsLetterOrDigit(firstChar).Should().BeTrue("a word-boundary trim must start on a whole word, never mid-word punctuation debris");
      }

      // ── HeroesToExpireOnClose: the briefing chat itself must never self-expire ──

      // The load-bearing case: the private-word conversation closing must NOT clear the very brief it just
      // produced, or it would never survive to reach the conversation it was meant for.
      [Test]
      public void GIVEN_the_closing_conversation_is_the_briefing_chat_itself_WHEN_resolving_expiry_THEN_nothing_expires()
      {
         var briefed = new List<string> {"olaf_id", "martira_id"};
         var participants = new List<string> {"olaf_id", "martira_id"};

         List<string> expired = CompanionBriefPolicy.HeroesToExpireOnClose(briefed, participants, isBriefingChat: true);

         expired.Should().BeEmpty();
      }

      // ── HeroesToExpireOnClose: an ordinary conversation spends the brief it carried ──

      // The brief is attached to the companion (ruling #2), so whatever ordinary conversation the briefed
      // companion actually takes part in next is "the conversation that follows": once it closes, the brief is
      // spent and must be cleared.
      [Test]
      public void GIVEN_a_briefed_companion_took_part_in_the_closed_conversation_WHEN_resolving_expiry_THEN_they_are_cleared()
      {
         var briefed = new List<string> {"olaf_id"};
         var participants = new List<string> {"target_lord_id", "olaf_id"};

         List<string> expired = CompanionBriefPolicy.HeroesToExpireOnClose(briefed, participants, isBriefingChat: false);

         expired.Should().ContainSingle().Which.Should().Be("olaf_id");
      }

      // A briefed companion who took no part in the conversation that just closed (still unused, still
      // carrying the player's word for whenever they DO get brought in) must not be swept up by an unrelated
      // conversation closing elsewhere.
      [Test]
      public void GIVEN_a_briefed_companion_did_not_take_part_in_the_closed_conversation_WHEN_resolving_expiry_THEN_they_survive()
      {
         var briefed = new List<string> {"olaf_id", "martira_id"};
         var participants = new List<string> {"target_lord_id", "olaf_id"}; // Martira was never brought into this one

         List<string> expired = CompanionBriefPolicy.HeroesToExpireOnClose(briefed, participants, isBriefingChat: false);

         expired.Should().NotContain("martira_id");
      }

      // No briefs outstanding at all: the common case (the feature unused this session) must be a cheap no-op,
      // never throw on empty input.
      [Test]
      public void GIVEN_no_companions_are_currently_briefed_WHEN_resolving_expiry_THEN_nothing_expires()
      {
         CompanionBriefPolicy.HeroesToExpireOnClose(new List<string>(), new List<string> {"target_lord_id"}, isBriefingChat: false)
            .Should().BeEmpty();
      }
   }
}
