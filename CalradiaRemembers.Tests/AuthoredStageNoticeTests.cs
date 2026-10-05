// Code written by Gabriel Mailhot, 04/10/2026.
// What the player is told when a written tale moves on. Gabriel (04/10/2026), after playing the werewolf tale:
// during a quest, a message should tell the player that a stage is complete and that a new stage begins. Until then
// the new stage only went into the journal, silently, so the player could miss that the story had moved at all.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredStageNoticeTests
   {
      // STAKE: the player learns three things in one line: which tale, that the step behind them is done, and what
      // the new one asks (the journal line of the new stage, names already filled).
      [Test]
      public void GIVEN_a_tale_moving_to_a_stage_with_a_journal_line_WHEN_the_player_is_told_THEN_they_read_the_tale_the_step_done_and_the_new_step()
      {
         AuthoredStageNotice.Text("The Wolf of the Wood", "Keep watch near Atrion at night.")
                            .Should().Be("Tale: The Wolf of the Wood. Step complete. New step: Keep watch near Atrion at night.");
      }

      // STAKE: a stage written without a journal line still moves the story; the player is told a step begins
      // rather than reading an empty "New step:".
      [Test]
      public void GIVEN_a_new_stage_without_a_journal_line_WHEN_the_player_is_told_THEN_they_read_that_a_new_step_begins()
      {
         AuthoredStageNotice.Text("The Wolf of the Wood", "  ").Should().Be("Tale: The Wolf of the Wood. Step complete. A new step begins.");
      }

      // STAKE: a journal line the author left without a full stop still reads as a sentence.
      [Test]
      public void GIVEN_a_journal_line_without_a_full_stop_WHEN_the_player_is_told_THEN_the_sentence_is_closed()
      {
         AuthoredStageNotice.Text("The Wolf of the Wood", "Report to Lucon").Should().EndWith("New step: Report to Lucon.");
         AuthoredStageNotice.Text("The Wolf of the Wood", "Who took them?").Should().EndWith("New step: Who took them?");
      }
   }
}
