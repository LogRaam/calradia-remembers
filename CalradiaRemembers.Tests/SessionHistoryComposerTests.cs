// Code written by Gabriel Mailhot, 18/07/2026.
// This composer rebuilds one participant's ChatSession from the shared attributed transcript before every
// LLM call, so what it decides IS the conversation as the model receives it. Get it wrong and the model
// either loses track of who said what, or reads its own past output in a shape it is not asked to produce.
// The rule lived inside ChatViewModel.BuildSessionFor until now, which meant it had NO test at all (the mod
// is net472, this project is net10).
//
// Each test below names the fault it guards.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SessionHistoryComposerTests
   {
      private const string Speaker = "lord_derthert";
      private const string Other = "lord_olaf";

      // The ordinary conversation, and the shape the overwhelming majority of turns must keep: plain
      // alternating messages, one per entry, nothing reshaped. The narration rebuild below is the exception,
      // and this pins that it stays one.
      [Test]
      public void GIVEN_a_plain_exchange_WHEN_composing_THEN_each_entry_stays_its_own_message()
      {
         IReadOnlyList<SessionTurn> turns = Compose(
            Player("Are you with me?"),
            Own("I am."),
            Player("Good."),
            Own("Say no more."));

         turns.Select(t => t.Role).Should()
              .Equal(SessionTurnRole.Player, SessionTurnRole.Speaker, SessionTurnRole.Player, SessionTurnRole.Speaker);
         turns.Select(t => t.Text).Should().Equal("Are you with me?", "I am.", "Good.", "Say no more.");
      }

      // Somebody else's line must reach the model ATTRIBUTED, never as this speaker's own assistant turn:
      // that is what lets an NPC answer what a witness actually said instead of believing they said it.
      [Test]
      public void GIVEN_a_line_from_another_participant_WHEN_composing_THEN_it_is_replayed_attributed_to_them()
      {
         SessionTurn turn = Compose(Line(Other, "Olaf", "He is lying to you.")).Single();

         turn.Role.Should().Be(SessionTurnRole.Witness);
         turn.SpeakerName.Should().Be("Olaf");
         turn.Text.Should().Be("He is lying to you.");
      }

      // Audit C4: a run of the speaker's own consecutive entries containing narration is rebuilt as ONE
      // assistant turn in the emitted [DIALOGUE]/[NARRATION] shape, so the model reads the format exemplified
      // in its own past output rather than learning it only from the system prompt.
      [Test]
      public void GIVEN_the_speakers_own_run_contains_narration_WHEN_composing_THEN_the_run_becomes_one_tagged_turn()
      {
         IReadOnlyList<SessionTurn> turns = Compose(
            Own("Kneel."),
            Narration("He circles the prisoner slowly."));

         turns.Should().HaveCount(1);
         turns[0].Role.Should().Be(SessionTurnRole.Speaker);
         turns[0].Text.Should().Be("[DIALOGUE]Kneel.[/DIALOGUE][NARRATION]He circles the prisoner slowly.[/NARRATION]");
      }

      // The rebuilt turn preserves the order the model EMITTED, because that order is itself the lesson:
      // a scene that opened on narration and then spoke must read back that way, or the next turn is taught
      // the opposite convention from the one it just used.
      [Test]
      public void GIVEN_narration_was_emitted_first_WHEN_composing_THEN_the_rebuilt_turn_keeps_that_order()
      {
         Compose(Narration("The door closes behind you."), Own("Sit."))
            .Single().Text
            .Should().Be("[NARRATION]The door closes behind you.[/NARRATION][DIALOGUE]Sit.[/DIALOGUE]");
      }

      // The boundary between the two shapes. A run WITHOUT narration must not be fused or tagged: tagging an
      // ordinary conversation would show the model markup it never used there, and fusing it would erase the
      // turn structure of the exchange.
      [Test]
      public void GIVEN_the_speakers_own_run_has_no_narration_WHEN_composing_THEN_it_is_left_untagged_and_unfused()
      {
         IReadOnlyList<SessionTurn> turns = Compose(Own("I am."), Own("And I will hold."));

         turns.Should().HaveCount(2);
         turns.Select(t => t.Text).Should().Equal("I am.", "And I will hold.");
      }

      // Only the speaker's OWN run is rebuilt. Another participant's narration-bearing lines stay attributed
      // statements, so the tagged format is never put in a mouth that did not produce it.
      [Test]
      public void GIVEN_narration_belongs_to_another_participant_WHEN_composing_THEN_it_is_not_folded_into_this_turn()
      {
         IReadOnlyList<SessionTurn> turns = Compose(
            Own("Kneel."),
            new TranscriptLine(false, Other, "Olaf", "Olaf looks away.", true));

         turns.Should().HaveCount(2);
         turns[0].Text.Should().Be("Kneel.");
         turns[1].Role.Should().Be(SessionTurnRole.Witness);
      }

      // Sprint 15C builds the NPC's session BEFORE the witness reactions are committed, so those reactions
      // reach the model only as the stimulus. Without the trim they would be counted twice: once in the
      // history, once in the message being answered.
      [Test]
      public void GIVEN_entries_are_excluded_WHEN_composing_THEN_the_last_ones_are_left_out()
      {
         Compose(new[] {Player("Speak freely."), Own("I shall."), Line(Other, "Olaf", "Then hear me.")}, excludeLastN: 1)
            .Select(t => t.Text).Should().Equal("Speak freely.", "I shall.");
      }

      // The boundary that pairs with the rebuild: a narration entry trimmed by excludeLastN must not be
      // pulled back into the run that precedes it. It would smuggle a line the caller deliberately withheld
      // into the very turn it was withheld from, tagged as already said.
      [Test]
      public void GIVEN_an_excluded_entry_would_extend_a_run_WHEN_composing_THEN_it_is_still_left_out()
      {
         Compose(new[] {Own("Kneel."), Narration("He circles the prisoner slowly.")}, excludeLastN: 1)
            .Single().Text
            .Should().Be("Kneel.");
      }

      // Fail safe: a speaker the engine has no hero for (a synthetic participant) claims NOTHING as its own.
      // Matching by name instead would be the dangerous alternative, letting one participant inherit
      // another's lines as their own assistant turns.
      [Test]
      public void GIVEN_the_speaker_has_no_hero_id_WHEN_composing_THEN_no_line_is_claimed_as_their_own()
      {
         SessionHistoryComposer.Compose(new[] {Own("I am.")}, "")
                               .Single().Role.Should().Be(SessionTurnRole.Witness);
         SessionHistoryComposer.Compose(new[] {Own("I am.")}, null)
                               .Single().Role.Should().Be(SessionTurnRole.Witness);
      }

      // Fail safe: nothing recorded yet is the state of every conversation's first turn.
      [Test]
      public void GIVEN_no_transcript_WHEN_composing_THEN_the_session_is_empty()
      {
         SessionHistoryComposer.Compose(null, Speaker).Should().BeEmpty();
         Compose().Should().BeEmpty();
      }

      // Fail safe: an excludeLastN larger than the transcript (or a negative one) must clamp, not throw or
      // read backwards. The caller computes it from a live count that another path may have already trimmed.
      [TestCase(5)]
      [TestCase(-1)]
      public void GIVEN_an_out_of_range_exclusion_WHEN_composing_THEN_it_is_clamped(int excludeLastN)
      {
         IReadOnlyList<SessionTurn> turns = Compose(new[] {Player("Speak freely."), Own("I shall.")}, excludeLastN);

         turns.Should().HaveCount(excludeLastN < 0
            ? 2
            : 0);
      }

      #region private

      private static IReadOnlyList<SessionTurn> Compose(params TranscriptLine[] lines)
         => SessionHistoryComposer.Compose(lines, Speaker);

      private static IReadOnlyList<SessionTurn> Compose(TranscriptLine[] lines, int excludeLastN)
         => SessionHistoryComposer.Compose(lines, Speaker, excludeLastN);

      private static TranscriptLine Player(string text) => new TranscriptLine(true, "player", "You", text);

      private static TranscriptLine Own(string text) => new TranscriptLine(false, Speaker, "Derthert", text);

      private static TranscriptLine Narration(string text) => new TranscriptLine(false, Speaker, "Derthert", text, true);

      private static TranscriptLine Line(string heroId, string name, string text)
         => new TranscriptLine(false, heroId, name, text);

      #endregion
   }
}
