// Code written by Gabriel Mailhot, 07/10/2026.
// tashmetu's tester notes on 2.7.3 (Nexus, 07/10/2026), fifteen written tales of his own:
// - "the interpreter (deepseek-v3.2) twice reported choice_voice for the choice voice, and TakeSaidChoice refused it
//   although the lord had plainly agreed ... The list reaches it as - choice voice: ..., so the model glues the two words";
// - "a finished tale stays listed twice, under Old Quests and still under Active Quests";
// - "once every required finding is found, the narrator keeps asking What do you do? until the player closes the chat".
// If these break, a choice the player plainly made is thrown away, the journal keeps a tale that ended, or a finished
// scene circles on forever.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleTesterNotesTests
   {
      private static readonly IReadOnlyList<string> Open = new[] {"voice", "stay_silent"};

      // tashmetu's case: the model glued the list's word to the id. The choice the player made is still found.
      [Test]
      public void GIVEN_a_choice_id_with_the_lists_word_glued_on_WHEN_matched_THEN_it_is_the_open_choice()
      {
         SaidChoiceId.Match("choice_voice", Open).Should().Be("voice");
         SaidChoiceId.Match("choice: voice", Open).Should().Be("voice");
         SaidChoiceId.Match("Choice Voice", Open).Should().Be("voice");
         SaidChoiceId.Match("\"stay_silent\"", Open).Should().Be("stay_silent");
      }

      // Only what is open: a near miss is never stretched into another choice.
      [Test]
      public void GIVEN_an_id_that_is_no_open_choice_WHEN_matched_THEN_nothing_is_taken()
      {
         SaidChoiceId.Match("choice_shout", Open).Should().BeNull();
         SaidChoiceId.Match("", Open).Should().BeNull();
         SaidChoiceId.Match("voice", new string[0]).Should().BeNull();
      }

      // The interpreter's list names the id alone, so there is no word to glue on.
      [Test]
      public void GIVEN_a_choice_by_words_WHEN_the_interpreter_is_told_THEN_its_line_starts_with_the_id_alone()
      {
         AuthoredQuest q = AuthoredTales.Load(AuthoredTales.Fork.Replace("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"said\": { \"to\": \"{giver}\", \"meaning\": \"the player gives up on the boy\" }"));
         AuthoredQuestRun run = AuthoredTales.At(q, "the_road");

         string block = AuthoredQuestPrompt.ChoicesForInterpreter(run, q, AuthoredTales.Giver);

         block.Should().Contain("- let_it_be: the player gives up on the boy").And.NotContain("- choice ");
      }

      // The journal entry of the tale under way stays; a second entry for the same tale is closed; a finished tale's entry
      // is closed the way it ended.
      [Test]
      public void GIVEN_journal_entries_of_tales_WHEN_reconciled_THEN_only_the_tale_under_way_keeps_one_open_entry()
      {
         TaleJournalPolicy.Verdict(new TaleJournalFacts {EntryStoryId = "t", RunQuestId = "t", RunStatus = AuthoredQuestStatus.Active, IsNewestOfItsStory = true})
                          .Should().Be(TaleJournalVerdict.Keep);
         TaleJournalPolicy.Verdict(new TaleJournalFacts {EntryStoryId = "t", RunQuestId = "t", RunStatus = AuthoredQuestStatus.Active, IsNewestOfItsStory = false})
                          .Should().Be(TaleJournalVerdict.CloseCancelled);
         TaleJournalPolicy.Verdict(new TaleJournalFacts {EntryStoryId = "t", RunQuestId = "t", RunStatus = AuthoredQuestStatus.Completed, IsNewestOfItsStory = true})
                          .Should().Be(TaleJournalVerdict.CloseCompleted);
         TaleJournalPolicy.Verdict(new TaleJournalFacts {EntryStoryId = "t", RunQuestId = "t", RunStatus = AuthoredQuestStatus.Failed, IsNewestOfItsStory = true})
                          .Should().Be(TaleJournalVerdict.CloseFailed);
         TaleJournalPolicy.Verdict(new TaleJournalFacts {EntryStoryId = "old", RunQuestId = "t", RunStatus = AuthoredQuestStatus.Active, IsNewestOfItsStory = true})
                          .Should().Be(TaleJournalVerdict.CloseCancelled);
      }

      // tashmetu's scene: everything required found and no way out to choose. The narrator is told the scene may end.
      [Test]
      public void GIVEN_every_required_finding_found_and_no_way_to_choose_WHEN_the_narrator_is_told_THEN_it_lets_the_scene_end()
      {
         AuthoredQuest q = AuthoredTales.Wolf();
         AuthoredQuestRun run = AuthoredTales.At(q, "the_tracks");
         foreach (AuthoredFinding f in AuthoredQuestEngine.Current(run, q).Objective.Scene.Findings)
            if (f.Required) AuthoredQuestEngine.Found(run, q, f.Id);

         AuthoredQuestEngine.SceneMayEnd(run, q).Should().BeTrue();
         AuthoredQuestPrompt.Narrator(run, q).Should().Contain("may leave");
      }

      // A scene with a required finding still hidden goes on as before.
      [Test]
      public void GIVEN_a_required_finding_still_hidden_WHEN_asked_THEN_the_scene_goes_on()
      {
         AuthoredQuest q = AuthoredTales.Wolf();
         AuthoredQuestRun run = AuthoredTales.At(q, "the_tracks");

         AuthoredQuestEngine.SceneMayEnd(run, q).Should().BeFalse();
         AuthoredQuestPrompt.Narrator(run, q).Should().NotContain("may leave");
      }
   }
}
