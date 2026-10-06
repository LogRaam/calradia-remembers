// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md; Gabriel, 04/10/2026: "with Twine you can give a quest several
// paths ... CR's quests must support that"). The loader decides whether a branching tale may be played, and says in
// plain words what is wrong with it: its report is what cr.story_check prints and what the tale editor shows. If it is
// wrong, a path that can never end strands the player mid-tale, a choice no one can take sits in the journal forever,
// or an author ships a tale whose ending, stage or flag silently never plays and is never told.

#region

using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredQuestBranchLoaderTests
   {
      private static string Fork(string from, string to)
      {
         AuthoredTales.Fork.Should().Contain(from);

         return AuthoredTales.Fork.Replace(from, to);
      }

      // The worked shape of a branching tale (acts, a scene with choices, flags, three endings) is playable: if the
      // loader refused it, no author could write a branch at all.
      [Test]
      public void GIVEN_a_tale_with_choices_flags_and_several_endings_WHEN_loaded_THEN_it_loads_with_nothing_to_report()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(AuthoredTales.Fork);

         r.Problems.Should().BeEmpty();
         r.Quests.Single().Endings.Keys.Should().Equal("rescued", "drowned", "abandoned");
      }

      // Two ways out with one name: the game could not tell which the player took. The author is told which stage.
      [Test]
      public void GIVEN_two_choices_of_one_stage_with_the_same_name_WHEN_loaded_THEN_the_tale_is_refused_naming_them()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"id\": \"give_up\"", "\"id\": \"ride_out\""));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("stage 'the_road': two choices are called 'ride_out'.") && p.EndsWith("The quest is not loaded."));
      }

      // A path toward an ending the file never wrote would end the tale with nothing to give: refused.
      [Test]
      public void GIVEN_a_choice_leading_to_an_ending_the_file_does_not_define_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"next\": \"end:drowned\"", "\"next\": \"end:lost\""));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("stage 'the_river' leads to ending 'lost', which \"endings\" does not define."));
      }

      // An ending nobody can reach is wasted writing, not a danger: the tale still loads, and the author is told.
      [Test]
      public void GIVEN_an_ending_no_path_leads_to_WHEN_loaded_THEN_the_author_is_told_and_the_tale_still_loads()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"next\": \"end:drowned\"", "\"next\": \"end:rescued\""));

         r.Quests.Should().HaveCount(1);
         r.Problems.Should().ContainSingle().Which.Should().EndWith("ending 'drowned' is never reached: no path leads to \"end:drowned\".");
      }

      // A loop with no exit strands the player for good: refused, naming the stages caught in it.
      [Test]
      public void GIVEN_two_stages_leading_only_to_each_other_WHEN_loaded_THEN_the_tale_is_refused_as_a_dead_end()
      {
         string json = Fork("\"on_complete\": { \"next\": \"end:drowned\" }", "\"on_complete\": { \"next\": \"the_mill\" }")
            .Replace("\"on_complete\": { \"next\": \"end:rescued\" }", "\"on_complete\": { \"next\": \"the_river\" }");

         AuthoredQuestLoadResult r = AuthoredTales.Read(json);

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("stage 'the_mill' can never reach an ending: every way out of it leads back without an exit."));
         r.Problems.Should().Contain(p => p.Contains("stage 'the_river' can never reach an ending"));
      }

      // A loop is fine when there is a way out of it (Gabriel's goal: "loops are now allowed when an exit exists"): the
      // player can turn back to the road as often as they like, since riding out still leads on.
      [Test]
      public void GIVEN_a_path_that_loops_back_but_has_a_way_out_WHEN_loaded_THEN_the_tale_loads()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"default\": true, \"sets\": \"turned_back\", \"next\": \"end:abandoned\"",
            "\"default\": true, \"sets\": \"turned_back\", \"next\": \"the_road\""));

         r.Quests.Should().HaveCount(1);
         r.Problems.Should().BeEmpty();
      }

      // A stage nothing leads to is never played; the author is told, and the tale still loads.
      [Test]
      public void GIVEN_a_stage_no_path_reaches_WHEN_loaded_THEN_the_author_is_told_it_is_never_played()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"next\": \"the_river\"", "\"next\": \"the_mill\""));

         r.Quests.Should().HaveCount(1);
         r.Problems.Should().Contain(p => p.EndsWith("stage 'the_river' is never reached from the first stage, so it is never played."));
      }

      // A scene left with no choice made must still go somewhere, or the player closes the window into nothing.
      [Test]
      public void GIVEN_a_scene_with_choices_but_no_way_out_when_closed_with_none_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"default\": true, ", ""));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("stage 'the_wood': a scene with choices needs a way out when it closes with none taken"));
      }

      // The default way out of a scene cannot hang on a flag: if it did not hold, the scene would close into nothing.
      [Test]
      public void GIVEN_the_default_way_out_of_a_scene_under_a_flag_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"default\": true,", "\"default\": true, \"if\": \"saw_light\","));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("the default choice 'turn_back' cannot depend on flags"));
      }

      // A flag tested but set nowhere never holds, so the choice or line under it is dead: the author is told which.
      [Test]
      public void GIVEN_a_flag_tested_but_never_set_WHEN_loaded_THEN_the_author_is_told_and_the_tale_still_loads()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"if\": \"saw_light\", \"next\": \"the_mill\"", "\"if\": \"saw_lights\", \"next\": \"the_mill\""));

         r.Quests.Should().HaveCount(1);
         r.Problems.Should().ContainSingle().Which.Should()
          .EndWith("flag 'saw_lights' is tested but never set anywhere, so \"saw_lights\" never holds and \"!saw_lights\" always does.");
      }

      // Keeping watch opens the scene of the stage that follows; as one way out among others it could never do that. The
      // author is told what a choice may do instead.
      [Test]
      public void GIVEN_a_choice_that_keeps_watch_WHEN_loaded_THEN_the_tale_is_refused_with_what_a_choice_may_do()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"objective\": { \"type\": \"keep_watch\", \"place\": \"{village}\", \"nights\": [1, 2], \"chance\": 0.5 }"));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("stage 'the_road', choice 'let_it_be' asks for 'keep_watch', which a choice cannot do")
                                          && p.Contains("talk_to, return, talk_to_locals, be_at, defeat_party, wait"));
      }

      // Outside a scene, a choice with no act and no words could never be taken: refused.
      [Test]
      public void GIVEN_a_choice_outside_a_scene_with_no_act_and_no_words_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"objective\": { \"type\": \"wait\", \"days\": 3 }, ", ""));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("choice 'let_it_be' has no way to be taken"));
      }

      // A choice made in a scene is listed to the player by its label and told to the narrator by it: without one there
      // is nothing to pick.
      [Test]
      public void GIVEN_a_choice_in_a_scene_without_a_label_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"label\": \"Take the right path down to the river\", ", ""));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("choice 'right' is made in a scene, so it needs a \"label\""));
      }

      // The first stage is the request, completed by accepting it in the offering conversation: it has no room for a
      // choice, and the author is told to move the choices on.
      [Test]
      public void GIVEN_the_request_itself_offering_choices_WHEN_loaded_THEN_the_tale_is_refused()
      {
         string json = Fork("\"on_complete\": { \"next\": \"the_road\" } },",
            "\"on_complete\": { \"next\": \"the_road\" }, \"choices\": [ { \"id\": \"now\", \"objective\": { \"type\": \"wait\", \"days\": 1 }, \"next\": \"the_road\" } ] },");

         AuthoredQuestLoadResult r = AuthoredTales.Read(json);

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("stage 'ask' is the request") && p.Contains("cannot offer choices"));
      }

      // Waiting no days at all would take the choice the instant the stage begins: refused.
      [Test]
      public void GIVEN_a_wait_of_no_days_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"days\": 3", "\"days\": 0"));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("choice 'let_it_be': wait needs \"days\" of at least 1."));
      }

      // Words said to a rival nobody bound could never be heard: refused, telling the author what to add.
      [Test]
      public void GIVEN_a_choice_said_to_a_rival_the_tale_does_not_bind_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"said\": { \"to\": \"{rival}\", \"meaning\": \"the player agrees to keep silent\" }"));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("choice 'let_it_be' is said to {rival}, but no rival is bound: add \"rival\" to \"requires\"."));
      }

      // One way to be taken per choice: an act AND words would leave the game two answers to one question.
      [Test]
      public void GIVEN_a_choice_with_both_an_act_and_words_WHEN_loaded_THEN_the_tale_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"objective\": { \"type\": \"wait\", \"days\": 3 }, \"said\": { \"to\": \"{giver}\", \"meaning\": \"the player gives up\" }"));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("choice 'let_it_be' has both an objective and a \"said\""));
      }

      // A tale whose every path is written as named endings must not be told its unused "reward" is missing, but a
      // reward someone wrote and nothing reaches is wasted, and the author is told.
      [Test]
      public void GIVEN_a_written_reward_no_path_reaches_WHEN_loaded_THEN_the_author_is_told()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"endings\": {", "\"reward\": { \"regard\": 5 }, \"endings\": {"));

         r.Quests.Should().HaveCount(1);
         r.Problems.Should().ContainSingle().Which.Should().Contain("the reward is never reached");
      }

      // A line may be plain or under a flag, and a canvas one line or several: what the author wrote is what is kept.
      [Test]
      public void GIVEN_lines_written_plain_and_under_flags_WHEN_loaded_THEN_each_keeps_its_words_and_its_flags()
      {
         AuthoredStage mill = AuthoredTales.Load().Stages.Single(s => s.Id == "the_mill");

         mill.Canvas.Select(l => l.Text).Should().Equal("The lord's relief is plain.", "He asks about the light in the wood.");
         mill.Canvas[1].If.Should().Equal("saw_light");
         mill.Knowers["{giver}"][2].If.Should().Equal("!saw_light");
      }

      // One broken way out is one problem: the author is told the path that leads nowhere, not a cascade of "can never
      // reach an ending" and "never reached" lines that only echo it and bury the real one.
      [Test]
      public void GIVEN_a_way_out_that_leads_nowhere_WHEN_loaded_THEN_the_author_is_told_that_one_problem_and_not_its_echoes()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"next\": \"the_wood\"", "\"next\": \"the_woods\""));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().ContainSingle().Which.Should().Contain("choice 'ride_out' leads to 'the_woods', which is no stage of this quest.");
      }

      // A file that opens with a comment is read like any other (it used to be read as holding nothing, silently).
      [Test]
      public void GIVEN_a_tale_file_that_opens_with_a_comment_WHEN_loaded_THEN_the_tale_loads()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read("// written by hand" + System.Environment.NewLine + AuthoredTales.Fork);

         r.Problems.Should().BeEmpty();
         r.Quests.Should().ContainSingle();
      }

      // A malformed condition is a malformed file, said in the same plain way, never thrown at the game.
      [Test]
      public void GIVEN_a_flag_written_as_a_number_WHEN_loaded_THEN_it_is_reported_as_invalid_not_thrown()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Fork("\"sets\": \"refused\"", "\"sets\": 7"));

         r.Quests.Should().BeEmpty();
         r.Problems.Should().ContainSingle().Which.Should().StartWith("test.json: not valid JSON (");
      }
   }
}
