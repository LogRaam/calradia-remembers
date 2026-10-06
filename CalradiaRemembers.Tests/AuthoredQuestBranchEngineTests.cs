// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales (docs/design/quest-editor/goal.md): the stage machine when a stage has several ways out. It decides
// which path the story takes from what the game saw (an act, a marker in a scene, words reported by the interpreter),
// which flags that path sets, and which ending the tale reaches; its answer feeds the journal, the prompts and the
// rewards. If it is wrong, two paths are taken at once, a closed path is taken anyway, the player's choice in a scene
// is lost or decided by a click instead of the story, an ending pays the wrong thing, or a save made before this
// change, or in the middle of a branch, no longer plays on.

#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AuthoredQuestBranchEngineTests
   {
      private const string Giver = AuthoredTales.Giver;
      private const string Village = AuthoredTales.Village;

      private static AuthoredQuest Tale(string from = null, string to = null)
         => AuthoredTales.Load(from == null ? AuthoredTales.Fork : AuthoredTales.Fork.Replace(from, to));

      /// <summary>A run accepted and standing at the road, where the three ways out begin.</summary>
      private static AuthoredQuestRun AtTheRoad(AuthoredQuest q)
      {
         AuthoredQuestRun run = AuthoredTales.Start(q);
         AuthoredQuestEngine.Accept(run, q, 100).ToStage.Should().Be("the_road");

         return run;
      }

      private static AuthoredQuestRun InTheWood(AuthoredQuest q)
      {
         AuthoredQuestRun run = AtTheRoad(q);
         AuthoredQuestEngine.Arrived(run, q, Village, 101).ToStage.Should().Be("the_wood");

         return run;
      }

      // Gabriel's goal: "the first choice whose objective is met wins; the others close". Riding to the village takes
      // that path, and going back to the lord afterwards no longer abandons the tale.
      [Test]
      public void GIVEN_a_stage_left_by_several_acts_WHEN_the_player_rides_to_the_village_first_THEN_that_path_is_taken_and_the_others_close()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredStep step = AuthoredQuestEngine.Arrived(run, q, Village, 101);

         step.ChoiceId.Should().Be("ride_out");
         run.StageId.Should().Be("the_wood");
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 102).Kind.Should().Be(AuthoredStepKind.None);
         run.Status.Should().Be(AuthoredQuestStatus.Active);
         run.ChoicesTaken.Should().Equal("the_road/ride_out");
      }

      // Going back to the lord instead is a path of its own: it ends the tale on its own ending, and sets its flag.
      [Test]
      public void GIVEN_a_stage_left_by_several_acts_WHEN_the_player_goes_back_to_the_lord_THEN_the_tale_ends_its_own_way_and_remembers_it()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredStep step = AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 101);

         step.Kind.Should().Be(AuthoredStepKind.Completed);
         step.EndingId.Should().Be("abandoned");
         run.Flags.Should().Contain("refused");
         AuthoredQuestEngine.Outcome(run, q).Regard.Should().Be(-4);
      }

      // Doing nothing is a choice too (goal: "N days pass with no other choice taken"), but only once those days are
      // over: on the second day the tale still waits for the player.
      [Test]
      public void GIVEN_days_passing_with_nothing_else_taken_WHEN_the_wait_is_over_THEN_the_waiting_path_is_taken_and_not_before()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredQuestEngine.Day(run, q, 102).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredStep step = AuthoredQuestEngine.Day(run, q, 103);

         step.ChoiceId.Should().Be("let_it_be");
         step.EndingId.Should().Be("abandoned");
      }

      // A closed choice can never be taken by an act: with its flag unset, meeting the lord leaves the road untouched.
      [Test]
      public void GIVEN_a_choice_by_act_closed_by_its_flag_WHEN_its_act_is_done_THEN_it_is_not_taken()
      {
         AuthoredQuest q = Tale("\"sets\": \"refused\", \"next\": \"end:abandoned\"", "\"if\": \"saw_light\", \"sets\": \"refused\", \"next\": \"end:abandoned\"");
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ForceChoice(run, q, "give_up", 101).Kind.Should().Be(AuthoredStepKind.None);
         run.StageId.Should().Be("the_road");
      }

      // In a scene, a path closed by its flag is not taken even if the narration writes its marker: the light was
      // never seen, so the left path does not exist for this player.
      [Test]
      public void GIVEN_a_scene_choice_closed_by_its_flag_WHEN_its_marker_is_written_THEN_it_is_refused()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = InTheWood(q);

         AuthoredQuestEngine.ChooseInScene(run, q, "left").Should().BeFalse();
         AuthoredQuestEngine.SceneChoices(run, q).Select(c => c.Id).Should().Equal("right", "turn_back");
      }

      // The same path opens once its flag is set in the scene (the light found), and its marker then takes the story
      // there when the scene closes.
      [Test]
      public void GIVEN_the_light_found_in_the_scene_WHEN_the_left_marker_is_written_THEN_the_story_follows_the_light_on_closing()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = InTheWood(q);

         AuthoredQuestEngine.Found(run, q, "lantern").Should().BeTrue();
         AuthoredQuestEngine.ChooseInScene(run, q, "left").Should().BeTrue();
         run.StageId.Should().Be("the_wood");
         AuthoredQuestEngine.SceneClosed(run, q, 102, out AuthoredStep step);

         step.ChoiceId.Should().Be("left");
         run.StageId.Should().Be("the_mill");
         run.Flags.Should().Contain("saw_light");
      }

      // Only a declared path counts: a marker the file does not know changes nothing, and closing the scene then
      // follows its declared default.
      [Test]
      public void GIVEN_an_undeclared_marker_WHEN_written_THEN_nothing_changes_and_closing_follows_the_default()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = InTheWood(q);

         AuthoredQuestEngine.ChooseInScene(run, q, "fly_away").Should().BeFalse();
         AuthoredQuestEngine.SceneClosed(run, q, 102, out AuthoredStep step);

         step.ChoiceId.Should().Be("turn_back");
         step.EndingId.Should().Be("abandoned");
         run.Flags.Should().Contain("turned_back");
      }

      // The first marker decides: a second one in the same scene is refused, so one narration cannot take two paths.
      [Test]
      public void GIVEN_a_path_already_chosen_in_the_scene_WHEN_another_marker_follows_THEN_the_first_stands()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = InTheWood(q);

         AuthoredQuestEngine.ChooseInScene(run, q, "right").Should().BeTrue();
         AuthoredQuestEngine.ChooseInScene(run, q, "turn_back").Should().BeFalse();
         AuthoredQuestEngine.SceneClosed(run, q, 102, out _);

         run.StageId.Should().Be("the_river");
      }

      // Every path reaches its own ending, each with its own regard, memory and consequences, and a consequence under a
      // flag only on the path where the flag was set.
      [Test]
      public void GIVEN_each_path_of_the_tale_WHEN_walked_to_its_end_THEN_each_leaves_its_own_ending()
      {
         AuthoredQuest q = Tale();

         AuthoredQuestRun lit = InTheWood(q);
         AuthoredQuestEngine.Found(lit, q, "lantern");
         AuthoredQuestEngine.ChooseInScene(lit, q, "left");
         AuthoredQuestEngine.SceneClosed(lit, q, 102, out _);
         AuthoredQuestEngine.ConversationOpened(lit, q, Giver, null, 104).EndingId.Should().Be("rescued");

         AuthoredQuestRun river = InTheWood(q);
         AuthoredQuestEngine.ChooseInScene(river, q, "right");
         AuthoredQuestEngine.SceneClosed(river, q, 102, out _);
         AuthoredQuestEngine.ConversationOpened(river, q, Giver, null, 104).EndingId.Should().Be("drowned");

         AuthoredQuestRun refused = AtTheRoad(q);
         AuthoredQuestEngine.ConversationOpened(refused, q, Giver, null, 101);

         AuthoredQuestEngine.Outcome(lit, q).Memory.Should().Contain("brought the boy");
         AuthoredQuestEngine.Consequences(lit, AuthoredQuestEngine.Outcome(lit, q)).Should().HaveCount(2);
         AuthoredQuestEngine.Outcome(river, q).Regard.Should().Be(2);
         AuthoredQuestEngine.Consequences(river, AuthoredQuestEngine.Outcome(river, q)).Should().BeEmpty();
         AuthoredQuestEngine.Consequences(refused, AuthoredQuestEngine.Outcome(refused, q)).Single().Text.Should().Contain("would not even go");
      }

      // A path that reached the mill without the light: the consequence under "saw_light" is not carried out.
      [Test]
      public void GIVEN_a_consequence_under_a_flag_never_set_on_this_path_WHEN_the_ending_is_reached_THEN_it_is_not_carried_out()
      {
         AuthoredQuest q = Tale("\"if\": \"saw_light\", \"next\": \"the_mill\"", "\"next\": \"the_mill\"");
         AuthoredQuestRun run = InTheWood(q);
         AuthoredQuestEngine.ChooseInScene(run, q, "left");
         AuthoredQuestEngine.SceneClosed(run, q, 102, out _);
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 104);

         AuthoredQuestEngine.Consequences(run, AuthoredQuestEngine.Outcome(run, q)).Select(c => c.Type).Should().Equal("memory");
      }

      // A file written before branches keeps its single success ending: "reward" is what the werewolf pays.
      [Test]
      public void GIVEN_a_tale_written_with_one_reward_WHEN_it_ends_THEN_the_reward_is_its_one_success_ending()
      {
         AuthoredQuest wolf = Wolf();
         AuthoredQuestRun run = AuthoredTales.Start(wolf);
         run.StageId = "return";

         AuthoredQuestEngine.ConversationOpened(run, wolf, Giver, null, 120).Kind.Should().Be(AuthoredStepKind.Completed);

         run.EndingId.Should().BeNull();
         AuthoredQuestEngine.Outcome(run, wolf).Should().BeSameAs(wolf.Reward);
      }

      // The time-out still fails a tale, even in the middle of a branch, and leaves the failure.
      [Test]
      public void GIVEN_a_branching_tale_past_its_time_WHEN_a_day_passes_THEN_it_fails_and_leaves_the_failure()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = InTheWood(q);

         AuthoredQuestEngine.Day(run, q, 100 + q.Failure.Days + 1).Kind.Should().Be(AuthoredStepKind.Failed);

         AuthoredQuestEngine.Outcome(run, q).Should().BeSameAs(q.Failure);
      }

      // A save made before this change carries no flag, choice or ending. It must load and play on from where it was:
      // here, mid-questioning with one villager heard, the second completes the stage as it always did.
      [Test]
      public void GIVEN_a_run_saved_before_branches_existed_WHEN_loaded_THEN_it_plays_on_from_where_it_was()
      {
         const string savedBefore = "{\"QuestId\":\"cr_wolf_of_the_wood\",\"BoundIds\":{\"giver\":\"lord_ortongard\",\"village\":\"village_ortongard\"},"
                                    + "\"BoundNames\":{\"giver\":\"Lord Aldric\",\"village\":\"Ortongard\"},\"StageId\":\"question_the_village\",\"StartDay\":100,"
                                    + "\"StageStartDay\":100,\"Status\":0,\"TalkedThisStage\":[\"notable_a\"],\"NightsWatched\":0,\"LastNightDay\":-1,"
                                    + "\"Findings\":[],\"HazardsSprung\":[],\"BandAlerted\":false,\"Met\":[],\"SpawnedPartyId\":null,"
                                    + "\"SpokenStageId\":null,\"SpokenHeroId\":null}";
         AuthoredQuest wolf = Wolf();
         AuthoredQuestRun run = AuthoredQuestRun.Deserialize(savedBefore);

         run.Should().NotBeNull();
         run.Flags.Should().BeEmpty();
         AuthoredQuestEngine.ConversationOpened(run, wolf, "notable_a", Village, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationOpened(run, wolf, "notable_b", Village, 101).ToStage.Should().Be("keep_watch");
      }

      // A branching run saved in the middle of a scene keeps its flags and the path already chosen: after the load the
      // scene closes onto the same path.
      [Test]
      public void GIVEN_a_branching_run_saved_mid_scene_WHEN_reloaded_THEN_its_flags_and_chosen_path_survive()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = InTheWood(q);
         AuthoredQuestEngine.Found(run, q, "lantern");
         AuthoredQuestEngine.ChooseInScene(run, q, "left");

         AuthoredQuestRun loaded = AuthoredQuestRun.Deserialize(run.Serialize());
         AuthoredQuestEngine.SceneClosed(loaded, q, 103, out _);

         loaded.Flags.Should().Equal("saw_light");
         loaded.ChoicesTaken.Should().Equal("the_road/ride_out", "the_wood/left");
         loaded.StageId.Should().Be("the_mill");
      }

      // The journal shows the situation, then each way out the author named (goal), and never a closed one.
      [Test]
      public void GIVEN_a_stage_with_choices_WHEN_it_begins_THEN_the_journal_shows_its_line_then_each_open_choices_line()
      {
         AuthoredQuest q = Tale("\"journal\": \"Or tell {giver} it is not your affair.\",", "\"journal\": \"Or tell {giver} it is not your affair.\", \"if\": \"saw_light\",");
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredQuestEngine.JournalLines(run, q).Should().Equal("The boy is lost somewhere near {village}.", "Ride to {village} and search the wood.");
      }

      // Words count only when the interpreter names a declared, open path said to THIS person: the wrong person, an
      // unknown path or a closed one change nothing, and the right one is taken.
      [Test]
      public void GIVEN_a_choice_by_words_WHEN_reported_for_the_wrong_person_an_unknown_path_or_the_right_one_THEN_only_the_right_one_counts()
      {
         AuthoredQuest q = Tale("\"objective\": { \"type\": \"wait\", \"days\": 3 }", "\"said\": { \"to\": \"{giver}\", \"meaning\": \"the player says the boy is not worth the trouble\" }");
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredQuestEngine.SaidChoicesFor(run, q, Giver).Select(c => c.Id).Should().Equal("let_it_be");
         AuthoredQuestEngine.SaidChoicesFor(run, q, "lord_elsewhere").Should().BeEmpty();
         AuthoredQuestEngine.Said(run, q, "lord_elsewhere", "let_it_be", 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.Said(run, q, Giver, "give_up", 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.Said(run, q, Giver, "made_up", 101).Kind.Should().Be(AuthoredStepKind.None);

         AuthoredQuestEngine.Said(run, q, Giver, "let_it_be", 101).EndingId.Should().Be("abandoned");
         run.SpokenChoiceId.Should().Be("let_it_be");
      }

      // A path closed by its flag cannot be taken by words either.
      [Test]
      public void GIVEN_a_choice_by_words_closed_by_its_flag_WHEN_reported_THEN_it_is_not_taken()
      {
         AuthoredQuest q = Tale("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"if\": \"saw_light\", \"said\": { \"to\": \"{giver}\", \"meaning\": \"the player says the boy is not worth the trouble\" }");
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredQuestEngine.SaidChoicesFor(run, q, Giver).Should().BeEmpty();
         AuthoredQuestEngine.Said(run, q, Giver, "let_it_be", 101).Kind.Should().Be(AuthoredStepKind.None);
      }

      // The game puts no cap of its own on how many actions one reply reports, so an interpreter that reports two
      // choices in one reply must not take two paths: the first moves the story on, and the second, no longer open
      // at the new stage, changes nothing.
      [Test]
      public void GIVEN_two_choices_by_words_reported_in_one_reply_WHEN_both_reach_the_game_THEN_only_the_first_is_taken()
      {
         AuthoredQuest q = AuthoredTales.Load(AuthoredTales.Fork
            .Replace("\"objective\": { \"type\": \"wait\", \"days\": 3 }", "\"said\": { \"to\": \"{giver}\", \"meaning\": \"the player says the boy is not worth the trouble\" }")
            .Replace("\"objective\": { \"type\": \"talk_to\", \"hero\": \"{giver}\" }, \"sets\": \"refused\"",
               "\"said\": { \"to\": \"{giver}\", \"meaning\": \"the player says it is not their affair\" }, \"sets\": \"refused\""));
         AuthoredQuestRun run = AtTheRoad(q);
         AuthoredQuestEngine.SaidChoicesFor(run, q, Giver).Select(c => c.Id).Should().Equal("give_up", "let_it_be");

         AuthoredQuestEngine.Said(run, q, Giver, "let_it_be", 101).EndingId.Should().Be("abandoned");
         AuthoredQuestEngine.Said(run, q, Giver, "give_up", 101).Kind.Should().Be(AuthoredStepKind.None);

         run.SpokenChoiceId.Should().Be("let_it_be");
         run.Flags.Should().NotContain("refused");
      }

      // Two ways out that question different people each count their own: two villagers heard take the path that asks
      // for two villagers.
      [Test]
      public void GIVEN_a_choice_that_questions_the_locals_WHEN_enough_of_them_are_heard_THEN_that_path_is_taken()
      {
         AuthoredQuest q = Tale("\"objective\": { \"type\": \"wait\", \"days\": 3 }", "\"objective\": { \"type\": \"talk_to_locals\", \"place\": \"{village}\", \"count\": 2 }");
         AuthoredQuestRun run = AtTheRoad(q);

         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_b", Village, 101).ChoiceId.Should().Be("let_it_be");
      }

      // A band spawned for one way out takes that way when the player beats it, and only that band does.
      [Test]
      public void GIVEN_a_choice_that_hunts_a_band_WHEN_the_player_beats_it_THEN_that_path_is_taken_and_no_other_band_counts()
      {
         AuthoredQuest q = Tale("\"objective\": { \"type\": \"wait\", \"days\": 3 }",
            "\"objective\": { \"type\": \"defeat_party\", \"spawn\": { \"id\": \"wolves\", \"size\": [3, 5], \"near\": \"{village}\" } }");
         AuthoredQuestRun run = AtTheRoad(q);
         AuthoredQuestEngine.BandsToHunt(run, q).Single().Choice.Id.Should().Be("let_it_be");
         run.ChoiceBands["let_it_be"] = "cr_wolves_1";

         AuthoredQuestEngine.PartyDefeated(run, q, "looters_9", 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.PartyDefeated(run, q, "cr_wolves_1", 101).ChoiceId.Should().Be("let_it_be");
      }

      private static AuthoredQuest Wolf()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string file = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData", "Quests", "wolf_of_the_wood.json");

         return AuthoredQuestLoader.Parse(File.ReadAllText(file), "wolf_of_the_wood.json").Quests.Single();
      }
   }
}
