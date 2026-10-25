// Code written by Gabriel Mailhot, 24/10/2026.

// A tale a scenario opens, judged by the regard it left, with a new video each time it comes back. Gabriel (24/10/2026):
// at night, a lady fetches the captive player to Rhagaea's chamber; "ca s'est bien passe si Rhagaea a gagne plus de
// Regards qu'elle en a perdu pendant l'histoire"; the memory stays either way, "plus neutre.. ou meme negatif si ca se
// passe mal"; "ce ne sera pas un conte unique", and each time "qu'on choisisse un nouveau video parmi les choix que je
// donnerai". These pin the tale engine's part: a tale only a scenario opens, a talk that completes as the conversation
// ends (so all of it counts), the two flags that say how the giver's regard moved, a regard only some endings earn, a
// chance that never fires on a typo, and a pool of pictures that never repeats one before every one is seen. If they
// break, the night is judged before it is played, a cold night leaves a warm memory, or the same video plays every time.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Scenarios;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleOpenedByScenarioTests
   {
      private const string Night = """
      {
        "id": "cr_test_night",
        "title": "A Summons by Night",
        "unique": false, "repeat_cooldown_days": 20,
        "requires": { "giver": { "role": "lord", "gender": "female", "min_regard": 1, "not_at_war_with_player": false }, "content": "hardcore" },
        "on_scenario": { "invitation": "A lady stands at your cell door: {giver} asks to see you.", "accept": "Follow her", "decline": "Stay in your cell", "decline_ending": "refused" },
        "binds": { "giver": "the lady who holds you" },
        "stages": [
          { "id": "summoned", "journal": "{giver} sent for you in the night.", "objective": { "type": "talk_to", "hero": "{giver}" },
            "on_complete": { "next": "chamber" } },
          { "id": "chamber", "journal": "The night in {giver}'s chamber.", "image_pool": [ "night_1.crvid", "night_2.crvid", "night_3.crvid" ],
            "objective": { "type": "talk_to", "hero": "{giver}", "when": "closed" },
            "on_complete": { "next": "end:dawn" } }
        ],
        "endings": {
          "dawn": { "journal": "You are taken back to your cell before dawn.",
                    "consequences": [
                      { "type": "memory", "who": "{giver}", "text": "A night with the prisoner, and our secret.", "if": "regard_rose" },
                      { "type": "regard", "amount": 5, "if": "regard_rose" },
                      { "type": "memory", "who": "{giver}", "text": "I sent for the prisoner; it came to nothing.", "if": [ "!regard_rose", "!regard_fell" ] },
                      { "type": "memory", "who": "{giver}", "text": "The prisoner I sent for shamed me.", "if": "regard_fell" } ] },
          "refused": { "regard": -2, "memory": "I sent for the prisoner, and they would not come." }
        }
      }
      """;

      // STAKE: a scenario-opened tale must read clean, its built-in flags never reported as "tested but never set".
      [Test]
      public void GIVEN_a_tale_only_a_scenario_opens_WHEN_read_THEN_it_loads_clean_with_its_built_in_flags()
      {
         AuthoredQuest q = AuthoredTales.Load(Night);

         q.OnScenario.Invitation.Should().Contain("{giver}");
         q.Stages[1].ImagePool.Should().HaveCount(3);
      }

      // STAKE (Gabriel, 25/10/2026: the player who will not follow the lady ends the tale, and she is disappointed): the
      // popup's no plays its ending at once, from the first stage.
      [Test]
      public void GIVEN_a_tale_whose_no_is_an_ending_WHEN_the_player_declines_THEN_the_tale_ends_there()
      {
         AuthoredQuest q = AuthoredTales.Load(Night);
         AuthoredQuestRun run = AuthoredTales.Start(q);

         AuthoredStep step = AuthoredQuestEngine.Declined(run, q, 101);

         step.Kind.Should().Be(AuthoredStepKind.Completed);
         AuthoredQuestEngine.Outcome(run, q).Memory.Should().Contain("would not come");
      }

      // STAKE: a no that names an ending the tale does not have would end it on nothing; the author is told.
      [Test]
      public void GIVEN_a_decline_ending_the_tale_lacks_WHEN_read_THEN_the_loader_refuses_it()
      {
         AuthoredTales.Read(Night.Replace("\"decline_ending\": \"refused\"", "\"decline_ending\": \"sulked\"")).Problems
                      .Should().Contain(p => p.Contains("decline_ending 'sulked'"));
      }

      // STAKE: a tale whose no costs nothing (no decline_ending) is left as it always was: the no ends nothing.
      [Test]
      public void GIVEN_a_tale_whose_no_costs_nothing_WHEN_the_player_declines_THEN_nothing_is_played()
      {
         AuthoredQuest q = AuthoredTales.Read(Night.Replace(", \"decline_ending\": \"refused\"", "")).Quests.Single();

         AuthoredQuestEngine.Declined(AuthoredTales.Start(q), q, 101).Kind.Should().Be(AuthoredStepKind.None);
      }

      // STAKE (Gabriel, 25/10/2026: the summons asked in role-play): a scenario tale may have no popup at all; a no that
      // names an ending is then never played, and the author is told.
      [Test]
      public void GIVEN_a_scenario_tale_without_an_invitation_WHEN_read_THEN_it_loads_and_an_unplayable_no_is_named()
      {
         AuthoredQuestLoadResult r = AuthoredTales.Read(Night.Replace("\"invitation\": \"A lady stands at your cell door: {giver} asks to see you.\", ", ""));

         r.Quests.Should().ContainSingle();
         r.Problems.Should().Contain(p => p.Contains("decline_ending is never played"));
      }

      // STAKE: one way in. A tale that says both how a scenario opens it and how it begins on the map would be offered by both.
      [Test]
      public void GIVEN_a_tale_with_both_on_map_and_on_scenario_WHEN_read_THEN_the_loader_refuses_it()
      {
         string both = Night.Replace("\"on_scenario\":", "\"on_map\": { \"invitation\": \"x\" }, \"on_scenario\":");

         AuthoredTales.Read(both).Problems.Should().Contain(p => p.Contains("on_scenario and on_map"));
      }

      // STAKE: a misspelt moment must not load as "opened" and end the night before it is played.
      [Test]
      public void GIVEN_a_talk_whose_moment_is_misspelt_WHEN_read_THEN_the_loader_refuses_it()
      {
         AuthoredTales.Read(Night.Replace("\"when\": \"closed\"", "\"when\": \"close\"")).Problems.Should().Contain(p => p.Contains("\"when\""));
      }

      // STAKE: a regard consequence that moves nothing, or past the ledger's bounds, is a mistake to tell the author of.
      [Test]
      public void GIVEN_a_regard_consequence_of_nothing_WHEN_read_THEN_the_loader_refuses_it()
      {
         AuthoredTales.Read(Night.Replace("\"amount\": 5", "\"amount\": 0")).Problems.Should().Contain(p => p.Contains("'regard'"));
      }

      // STAKE (Gabriel: the night judged by what it left): the talk that ends on the close does not complete as the chat
      // opens, so everything said in it counts; it completes when the conversation ends.
      [Test]
      public void GIVEN_a_talk_that_completes_on_the_close_WHEN_the_conversation_opens_then_ends_THEN_only_the_end_completes_it()
      {
         AuthoredQuest q = AuthoredTales.Load(Night);
         AuthoredQuestRun run = AuthoredTales.At(q, "chamber");

         AuthoredQuestEngine.ConversationOpened(run, q, AuthoredTales.Giver, null, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationEnded(run, q, "someone_else", 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredStep step = AuthoredQuestEngine.ConversationEnded(run, q, AuthoredTales.Giver, 101);

         step.Kind.Should().Be(AuthoredStepKind.Completed);
         step.EndingId.Should().Be("dawn");
      }

      // STAKE: Rhagaea must know the scene she is in for the whole night: with nothing completed as the chat opened, the
      // giver of a talk that ends on the close still speaks the stage's moment and its facts.
      [Test]
      public void GIVEN_a_talk_that_completes_on_the_close_WHEN_its_giver_is_spoken_to_THEN_she_is_told_the_stages_moment()
      {
         string withMoment = Night.Replace("\"objective\": { \"type\": \"talk_to\", \"hero\": \"{giver}\", \"when\": \"closed\" },",
            "\"objective\": { \"type\": \"talk_to\", \"hero\": \"{giver}\", \"when\": \"closed\" }, \"canvas\": \"Night in your chamber.\", \"facts\": [ \"Guards wait at the door.\" ],");
         AuthoredQuest q = AuthoredTales.Load(withMoment);
         AuthoredQuestRun run = AuthoredTales.At(q, "chamber");
         AuthoredQuestEngine.ConversationOpened(run, q, AuthoredTales.Giver, null, 101);

         string block = AuthoredQuestPrompt.ForCharacter(run, q, AuthoredTales.Giver, null, offering: false);

         block.Should().Contain("Night in your chamber.").And.Contain("Guards wait at the door.");
      }

      // STAKE: every tale written before still completes a talk as the conversation opens; the close changes nothing for it.
      [Test]
      public void GIVEN_an_ordinary_talk_WHEN_the_conversation_ends_THEN_it_was_already_met_on_the_opening_and_the_end_adds_nothing()
      {
         AuthoredQuest q = AuthoredTales.Load(Night);
         AuthoredQuestRun run = AuthoredTales.Start(q);

         AuthoredQuestEngine.ConversationEnded(run, q, AuthoredTales.Giver, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationOpened(run, q, AuthoredTales.Giver, null, 101).Kind.Should().Be(AuthoredStepKind.Advanced);
      }

      // STAKE (Gabriel: "si Rhagaea a gagne plus de Regards qu'elle en a perdu"): a warmer giver ends with regard_rose, a
      // colder one with regard_fell, an unmoved one with neither, so the neutral memory is the one told.
      [Test]
      public void GIVEN_the_givers_regard_at_the_start_WHEN_the_tale_ends_higher_lower_or_level_THEN_the_one_matching_flag_is_set()
      {
         AuthoredQuestRun Run(int start) => new() {GiverRegardAtStart = start};

         AuthoredQuestRun rose = Run(10), fell = Run(10), level = Run(10);
         TaleRegardFlags.Mark(rose, 14);
         TaleRegardFlags.Mark(fell, 3);
         TaleRegardFlags.Mark(level, 10);

         rose.Flags.Should().Equal(TaleRegardFlags.Rose);
         fell.Flags.Should().Equal(TaleRegardFlags.Fell);
         level.Flags.Should().BeEmpty();
      }

      // STAKE: a run saved before the start was noted (an older save) is never judged on a regard it never read.
      [Test]
      public void GIVEN_a_run_that_never_noted_the_start_WHEN_the_tale_ends_THEN_no_regard_flag_is_set()
      {
         var run = new AuthoredQuestRun();

         TaleRegardFlags.Mark(run, 50);

         run.Flags.Should().BeEmpty();
      }

      // STAKE: the three memories and the reward each follow the flag the night set; a warm memory never follows a cold night.
      [Test]
      public void GIVEN_each_way_the_night_went_WHEN_the_ending_is_carried_out_THEN_only_its_memory_and_its_reward_hold()
      {
         AuthoredQuest q = AuthoredTales.Load(Night);
         AuthoredOutcome dawn = q.Endings["dawn"];

         IReadOnlyList<AuthoredConsequence> Held(params string[] flags)
            => AuthoredQuestEngine.Consequences(new AuthoredQuestRun {Flags = flags.ToList()}, dawn);

         Held(TaleRegardFlags.Rose).Select(c => c.Type).Should().Equal("memory", "regard");
         Held(TaleRegardFlags.Rose).First().Text.Should().Contain("our secret");
         Held().Single().Text.Should().Contain("came to nothing");
         Held(TaleRegardFlags.Fell).Single().Text.Should().Contain("shamed me");
      }

      // STAKE (Gabriel: "un nouveau video" each time): a picture already shown is never picked again before every one was.
      [Test]
      public void GIVEN_a_pool_partly_shown_WHEN_a_picture_is_picked_THEN_it_is_one_not_shown_yet()
      {
         string[] pool = {"night_1.crvid", "night_2.crvid", "night_3.crvid"};

         foreach (double roll in new[] {0.0, 0.5, 0.99})
            TaleImagePool.Pick(pool, new[] {"night_1.crvid", "night_3.crvid"}, roll).Should().Be("night_2.crvid");
      }

      // STAKE: once every video was seen, the tale still shows one, and a new round begins from it.
      [Test]
      public void GIVEN_every_picture_shown_WHEN_one_is_picked_THEN_a_new_round_begins()
      {
         string[] pool = {"night_1.crvid", "night_2.crvid"};
         var seen = new List<string> {"night_1.crvid", "night_2.crvid"};

         string picked = TaleImagePool.Pick(pool, seen, 0.0);

         picked.Should().NotBeNull();
         TaleImagePool.AfterShowing(pool, seen, picked).Should().Equal(picked);
      }

      // STAKE: three playings of a three-video pool show the three videos, each once.
      [Test]
      public void GIVEN_a_pool_of_three_WHEN_the_tale_is_played_three_times_THEN_each_video_is_shown_once()
      {
         string[] pool = {"a.crvid", "b.crvid", "c.crvid"};
         var seen = new List<string>();
         var shown = new List<string>();
         foreach (double roll in new[] {0.7, 0.7, 0.7})
         {
            string picked = TaleImagePool.Pick(pool, seen, roll);
            shown.Add(picked);
            seen = TaleImagePool.AfterShowing(pool, seen, picked);
         }

         shown.Should().BeEquivalentTo(pool);
      }

      // STAKE: the stage shows the video chosen for this playing, not its plain image, for the whole tale.
      [Test]
      public void GIVEN_a_picture_chosen_from_the_pool_WHEN_the_stage_shows_its_image_THEN_it_is_the_chosen_one()
      {
         AuthoredQuest q = AuthoredTales.Load(Night);
         AuthoredQuestRun run = AuthoredTales.At(q, "chamber");
         run.ImagePicks["chamber"] = "night_2.crvid";

         AuthoredTaleImages.ImageFor(run, q).Should().Be("night_2.crvid");
      }

      // STAKE: IF_CHANCE:0.25 fires about a night in four; a chance that is no number never fires, rather than always.
      [Test]
      public void GIVEN_a_chance_WHEN_rolled_THEN_it_holds_below_the_chance_and_never_on_a_typo()
      {
         ScenarioArgs.ChanceHolds("0.25", 0.10).Should().BeTrue();
         ScenarioArgs.ChanceHolds("0.25", 0.30).Should().BeFalse();
         ScenarioArgs.ChanceHolds("one in four", 0.0).Should().BeFalse();
         ScenarioArgs.ChanceHolds("0", 0.0).Should().BeFalse();
      }
   }
}
