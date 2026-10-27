// Code written by Gabriel Mailhot, 27/10/2026.
// TaleHistory turns the endings a tale already reached with a giver into flags of the run that begins again with them
// (Gabriel, 26/10/2026: "si c'est la deuxieme fois ou plus que l'histoire se joue, il faudrait que Rhagaea se souvienne
// des autres fois et que ca soit une continuite"). If it is wrong, Rhagaea greets as a stranger a prisoner she already
// took to her bed, or speaks of a night she never had, or a tale's lines under those flags are refused as dead.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleHistoryTests
   {
      // STAKE: the first night is a first night: nothing is remembered that never happened.
      [Test]
      public void GIVEN_a_tale_never_played_with_this_giver_WHEN_it_begins_THEN_it_carries_no_history()
      {
         var run = new AuthoredQuestRun {QuestId = "rhagaea_night"};
         TaleHistory.Mark(run, new List<string>());

         run.Flags.Should().NotContain(TaleHistory.PlayedBefore);
      }

      // STAKE: Gabriel's case: a second night with Rhagaea follows the first, which ended at dawn.
      [Test]
      public void GIVEN_a_night_that_ended_at_dawn_WHEN_the_tale_begins_again_with_her_THEN_it_knows_it()
      {
         var run = new AuthoredQuestRun {QuestId = "rhagaea_night"};
         TaleHistory.Mark(run, TaleHistory.After(null, "dawn"));

         run.Flags.Should().Contain(TaleHistory.PlayedBefore).And.Contain("ended_before:dawn").And.NotContain("ended_before:refused");
      }

      // STAKE: a night refused once and spent once both count; each ending is kept once however often it came.
      [Test]
      public void GIVEN_endings_reached_more_than_once_WHEN_kept_THEN_each_is_kept_once()
      {
         List<string> history = TaleHistory.After(TaleHistory.After(TaleHistory.After(null, "refused"), "dawn"), "dawn");

         history.Should().Equal("refused", "dawn");
      }

      // STAKE: what was lived with one giver is not another's memory: the history is kept per giver.
      [Test]
      public void GIVEN_two_givers_WHEN_their_histories_are_keyed_THEN_the_keys_differ()
      {
         TaleHistory.Key("rhagaea_night", "lord_1_14").Should().NotBe(TaleHistory.Key("rhagaea_night", "lord_1_15"));
      }

      // STAKE: a reward and a failure are endings too, named so a line can test them.
      [Test]
      public void GIVEN_the_reward_or_the_failure_WHEN_named_for_the_history_THEN_they_read_as_reward_and_failure()
      {
         TaleHistory.EndingName(null, false).Should().Be("reward");
         TaleHistory.EndingName("dawn", false).Should().Be("dawn");
         TaleHistory.EndingName("dawn", true).Should().Be("failure");
      }

      // STAKE: a line under "ended_before:dawn" is not refused by the loader as testing a flag set nowhere.
      [Test]
      public void GIVEN_a_tale_with_endings_WHEN_its_history_flags_are_listed_THEN_each_ending_has_one()
      {
         var q = new AuthoredQuest {Endings = new Dictionary<string, AuthoredOutcome> {["dawn"] = new(), ["refused"] = new()}};

         TaleHistory.FlagsOf(q).Should().Contain(new[] {"played_before", "ended_before:dawn", "ended_before:refused", "ended_before:reward", "ended_before:failure"});
      }
   }
}
