// Code written by Gabriel Mailhot, 25/10/2026.
// TalePause decides when a tale under way is set aside for a scenario's tale (Gabriel, 25/10/2026: the wolf's tale under
// way when Rhagaea sends for her prisoner), when it is taken up again, and that its clock stops while it waits. If it is
// wrong, the night never opens while another tale plays, a third tale starts on top of two, the wolf fails for days spent
// in a cell, or its journal entry is closed while it waits.

#region

using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TalePauseTests
   {
      // STAKE: Gabriel's case: on the wolf's trail, taken prisoner by Rhagaea, the night must still open.
      [Test]
      public void GIVEN_a_tale_under_way_and_none_set_aside_WHEN_a_scenario_summons_THEN_nothing_stands_in_the_way()
      {
         TalePause.StoryUnderWay(new TaleMoment {RunActive = true, OnePaused = false, BySummons = true}).Should().BeFalse();
      }

      // STAKE: one level only ("oui ca me va"): a summons never sets a second tale aside while one already waits.
      [Test]
      public void GIVEN_a_tale_set_aside_and_another_under_way_WHEN_a_scenario_summons_THEN_it_waits()
      {
         TalePause.StoryUnderWay(new TaleMoment {RunActive = true, OnePaused = true, BySummons = true}).Should().BeTrue();
      }

      // STAKE: the night is over but the player is still held: a second night may come, nothing more is set aside.
      [Test]
      public void GIVEN_a_tale_set_aside_and_nothing_under_way_WHEN_a_scenario_summons_THEN_it_may_open()
      {
         TalePause.StoryUnderWay(new TaleMoment {RunActive = false, OnePaused = true, BySummons = true}).Should().BeFalse();
      }

      // STAKE: a tale raised in conversation never interrupts: it waits for the tale under way and the one set aside.
      [Test]
      public void GIVEN_a_tale_under_way_or_set_aside_WHEN_a_tale_is_raised_in_conversation_THEN_it_waits()
      {
         TalePause.StoryUnderWay(new TaleMoment {RunActive = true, OnePaused = false, BySummons = false}).Should().BeTrue();
         TalePause.StoryUnderWay(new TaleMoment {RunActive = false, OnePaused = true, BySummons = false}).Should().BeTrue();
         TalePause.StoryUnderWay(new TaleMoment {RunActive = false, OnePaused = false, BySummons = false}).Should().BeFalse();
      }

      // STAKE: "reprendre ensuite la quete du loup lorsque je suis libere de prison": not before.
      [Test]
      public void GIVEN_a_tale_set_aside_WHEN_asked_whether_it_takes_up_again_THEN_only_once_nothing_plays_and_the_player_is_free()
      {
         TalePause.ShouldResume(new TaleMoment {OnePaused = true, RunActive = false, PlayerPrisoner = false}).Should().BeTrue();
         TalePause.ShouldResume(new TaleMoment {OnePaused = true, RunActive = false, PlayerPrisoner = true}).Should().BeFalse();
         TalePause.ShouldResume(new TaleMoment {OnePaused = true, RunActive = true, PlayerPrisoner = false}).Should().BeFalse();
         TalePause.ShouldResume(new TaleMoment {OnePaused = false, RunActive = false, PlayerPrisoner = false}).Should().BeFalse();
      }

      // STAKE: Gabriel agreed the clock stops while the tale waits: days in a cell never make the wolf fail.
      [Test]
      public void GIVEN_a_tale_set_aside_for_twelve_days_WHEN_it_takes_up_again_THEN_its_days_are_counted_from_where_they_stood()
      {
         var run = new AuthoredQuestRun {QuestId = "wolf", StartDay = 100, StageStartDay = 104};
         TalePause.Pause(run, 110);
         run.PausedOnDay.Should().Be(110);

         TalePause.Resume(run, 122);

         run.StartDay.Should().Be(112);
         run.StageStartDay.Should().Be(116);
         run.PausedOnDay.Should().BeNull();
      }

      // STAKE: a run saved before a pause, or never set aside, is taken up as it stands.
      [Test]
      public void GIVEN_a_run_never_set_aside_WHEN_taken_up_THEN_its_days_do_not_move()
      {
         var run = new AuthoredQuestRun {QuestId = "wolf", StartDay = 100, StageStartDay = 104};
         TalePause.Resume(run, 122);

         run.StartDay.Should().Be(100);
         run.StageStartDay.Should().Be(104);
      }

      // STAKE: the day it was set aside survives a save, or a loaded game would count the days in the cell after all.
      [Test]
      public void GIVEN_a_run_set_aside_WHEN_saved_and_read_back_THEN_the_day_it_was_set_aside_is_kept()
      {
         var run = new AuthoredQuestRun {QuestId = "wolf", StartDay = 100, StageStartDay = 104};
         TalePause.Pause(run, 110);

         AuthoredQuestRun.Deserialize(run.Serialize()).PausedOnDay.Should().Be(110);
      }

      // STAKE: the wolf's journal entry stays open while it waits; a closed quest cannot be reopened.
      [Test]
      public void GIVEN_the_journal_entry_of_a_tale_set_aside_WHEN_reconciled_THEN_it_stays_open()
      {
         TaleJournalPolicy.Verdict(new TaleJournalFacts {
            EntryStoryId = "wolf", RunQuestId = "night", RunStatus = AuthoredQuestStatus.Active, IsNewestOfItsStory = true, PausedQuestId = "wolf"
         }).Should().Be(TaleJournalVerdict.Keep);
         TaleJournalPolicy.Verdict(new TaleJournalFacts {
            EntryStoryId = "wolf", RunQuestId = null, RunStatus = null, IsNewestOfItsStory = true, PausedQuestId = "wolf"
         }).Should().Be(TaleJournalVerdict.Keep);
      }

      // STAKE: a stray older copy of the waiting tale's entry is still closed (tashmetu: a tale listed twice).
      [Test]
      public void GIVEN_an_older_copy_of_the_entry_of_a_tale_set_aside_WHEN_reconciled_THEN_it_is_closed()
      {
         TaleJournalPolicy.Verdict(new TaleJournalFacts {
            EntryStoryId = "wolf", RunQuestId = "night", RunStatus = AuthoredQuestStatus.Active, IsNewestOfItsStory = false, PausedQuestId = "wolf"
         }).Should().Be(TaleJournalVerdict.CloseCancelled);
      }
   }
}
