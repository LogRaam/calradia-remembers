// Code written by Gabriel Mailhot, 10/11/2026.
// What QuestRepeatPolicy decides: whether a task offered repeats one the player already carries or has just done, and
// which tasks a war council is told of. If it is wrong, a second council sets the scouting a first saw done (bdionysis80,
// Nexus 10/11/2026: "Scout an army of Sturgia" twice, Athina then Vamina), or an honest new task is refused.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class QuestRepeatPolicyTests
   {
      private static readonly string Sturgia = QuestRepeatPolicy.KeyOf("sturgia", null, null);

      private static HeldQuest Scout(string giver, bool open = false, int? doneOn = null)
         => new() {Type = QuestType.ScoutArmy, TargetKey = Sturgia, GiverName = giver, Deed = "Scout an army of Sturgia", Open = open, DoneOnDay = doneOn,
                   Evidence = doneOn == null ? null : "1,257 men under Olek"};

      // STAKE (bdionysis80): Vamina's scouting of the army Athina's was done on three days before is not set again.
      [Test]
      public void GIVEN_the_same_scouting_done_three_days_ago_by_another_giver_WHEN_it_is_offered_again_THEN_it_is_a_repeat()
      {
         HeldQuest first = Scout("Athina", doneOn: 100);

         QuestRepeatPolicy.RepeatOf(QuestType.ScoutArmy, Sturgia, 103, new[] {first}).Should().BeSameAs(first);
      }

      // STAKE: the same deed still open, set by anyone, is never set a second time.
      [Test]
      public void GIVEN_the_same_deed_still_open_WHEN_it_is_offered_again_THEN_it_is_a_repeat()
      {
         QuestRepeatPolicy.RepeatOf(QuestType.ScoutArmy, Sturgia, 200, new[] {Scout("Athina", open: true)}).Should().NotBeNull();
      }

      // STAKE: news goes stale; a week after, a fresh look at the same realm's armies is a new task.
      [Test]
      public void GIVEN_the_same_deed_done_more_than_a_week_ago_WHEN_it_is_offered_again_THEN_it_is_a_new_task()
      {
         QuestRepeatPolicy.RepeatOf(QuestType.ScoutArmy, Sturgia, 100 + QuestRepeatPolicy.RepeatWindowDays + 1, new[] {Scout("Athina", doneOn: 100)})
                          .Should().BeNull();
      }

      // STAKE: another realm, or another deed against the same realm, is a different task.
      [Test]
      public void GIVEN_another_target_or_another_deed_WHEN_offered_THEN_it_is_a_new_task()
      {
         var held = new[] {Scout("Athina", doneOn: 100)};

         QuestRepeatPolicy.RepeatOf(QuestType.ScoutArmy, QuestRepeatPolicy.KeyOf("nord", null, null), 101, held).Should().BeNull();
         QuestRepeatPolicy.RepeatOf(QuestType.AttackFaction, Sturgia, 101, held).Should().BeNull();
      }

      // THE REPORT (Aporo, Discord, 14/11/2026): two bandit camps near Iyakis are two tasks, the second not refused as the first.
      [Test]
      public void GIVEN_a_cleared_hideout_near_a_town_WHEN_another_hideout_near_it_is_offered_THEN_it_is_not_a_repeat()
      {
         var held = new[] {
            new HeldQuest {Type = QuestType.BanditHideout, TargetKey = QuestRepeatPolicy.KeyOf(null, null, "Iyakis", "hideout_seaside_3"), DoneOnDay = 100}
         };

         QuestRepeatPolicy.RepeatOf(QuestType.BanditHideout, QuestRepeatPolicy.KeyOf(null, null, "Iyakis", "hideout_seaside_7"), 101, held).Should().BeNull();
      }

      // STAKE: the same hideout, still open, is not set a second time.
      [Test]
      public void GIVEN_an_open_task_on_a_hideout_WHEN_the_same_hideout_is_offered_THEN_it_is_a_repeat()
      {
         string key = QuestRepeatPolicy.KeyOf(null, null, "Iyakis", "hideout_seaside_3");
         var held = new[] {new HeldQuest {Type = QuestType.BanditHideout, TargetKey = key, Open = true}};

         QuestRepeatPolicy.RepeatOf(QuestType.BanditHideout, key, 101, held).Should().NotBeNull();
      }

      // STAKE: a hideout task from before the fix (no hideout in its key) does not block a new one on another camp.
      [Test]
      public void GIVEN_a_hideout_task_done_before_the_fix_WHEN_a_hideout_near_the_same_town_is_offered_THEN_it_is_not_a_repeat()
      {
         var held = new[] {new HeldQuest {Type = QuestType.BanditHideout, TargetKey = QuestRepeatPolicy.KeyOf(null, null, "Iyakis"), DoneOnDay = 100}};

         QuestRepeatPolicy.RepeatOf(QuestType.BanditHideout, QuestRepeatPolicy.KeyOf(null, null, "Iyakis", "hideout_seaside_7"), 101, held).Should().BeNull();
      }

      // STAKE: a task with no target (goods, coin) is never refused as a repeat; two loads of goods are two tasks.
      [Test]
      public void GIVEN_a_task_with_no_target_WHEN_offered_THEN_it_is_never_a_repeat()
      {
         QuestRepeatPolicy.KeyOf(null, " ", null).Should().BeEmpty();
         QuestRepeatPolicy.RepeatOf(QuestType.ProvideGoods, "", 101, new[] {new HeldQuest {Type = QuestType.ProvideGoods, TargetKey = "", Open = true}})
                          .Should().BeNull();
      }

      // STAKE (bdionysis80): the council reads what was already set and found, open tasks first.
      [Test]
      public void GIVEN_an_open_task_and_a_done_one_WHEN_the_council_lines_are_written_THEN_both_show_open_first_with_what_was_found()
      {
         var held = new List<HeldQuest> {Scout("Athina", doneOn: 100), new() {Type = QuestType.Siege, Deed = "Take Hvalvik by storm", GiverName = "Derthert", Open = true}};

         QuestRepeatPolicy.CouncilLines(held, 102).Should().Equal(
            "Derthert: take Hvalvik by storm, still open",
            "Athina: scout an army of Sturgia, done: 1,257 men under Olek");
      }

      // STAKE: old history does not crowd the table; a task done long ago is not listed.
      [Test]
      public void GIVEN_a_task_done_long_ago_WHEN_the_council_lines_are_written_THEN_it_is_left_out()
      {
         QuestRepeatPolicy.CouncilLines(new[] {Scout("Athina", doneOn: 10)}, 100).Should().BeEmpty();
      }
   }
}
