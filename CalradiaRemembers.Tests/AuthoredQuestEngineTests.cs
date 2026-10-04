// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: the stage machine, walked on the werewolf quest CR ships (Gabriel's story,
// docs/design/AUTHORED_QUESTS.md). Every stage moves only on a fact the game reports. If these rules are wrong, a quest
// advances on the model's say-so, strands the player at a stage that cannot end, makes him wait forever for a scene,
// rerolls a night on reload, or loses a clue because he took another path through the wood.

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
   public sealed class AuthoredQuestEngineTests
   {
      private const string Giver = "lord_ortongard";
      private const string Village = "village_ortongard";

      private static AuthoredQuest Wolf()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string file = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData", "Quests", "wolf_of_the_wood.json");

         return AuthoredQuestLoader.Parse(File.ReadAllText(file), "wolf_of_the_wood.json").Quests.Single();
      }

      private static AuthoredQuestRun Bound(AuthoredQuest q, int today = 100)
         => AuthoredQuestEngine.Start(q,
            new Dictionary<string, string> {["giver"] = Giver, ["village"] = Village},
            new Dictionary<string, string> {["giver"] = "Lord Aldric", ["village"] = "Ortongard"}, today);

      // The whole of Gabriel's story, each stage moved only by what the game saw.
      [Test]
      public void GIVEN_the_werewolf_quest_WHEN_played_through_THEN_each_stage_moves_on_a_fact_and_it_ends_rewarded()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q);

         AuthoredQuestEngine.Accept(run, q, 100).ToStage.Should().Be("question_the_village");
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_b", Village, 101).ToStage.Should().Be("keep_watch");

         AuthoredStep night = AuthoredStep.Nothing;
         for (int day = 102; day < 110 && night.Kind != AuthoredStepKind.OpenScene; day++)
            night = AuthoredQuestEngine.NightPassed(run, q, true, day);
         night.Kind.Should().Be(AuthoredStepKind.OpenScene);
         run.StageId.Should().Be("the_tracks");

         AuthoredQuestEngine.Found(run, q, "chains").Should().BeTrue();
         AuthoredQuestEngine.SceneClosed(run, q, 108, out AuthoredStep closed);
         closed.ToStage.Should().Be("tell_the_lord");

         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 109).ToStage.Should().Be("hunt_the_slavers");
         run.SpawnedPartyId = "cr_slavers_1";
         AuthoredQuestEngine.PartyDefeated(run, q, "cr_slavers_1", 112).ToStage.Should().Be("return");
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 113).Kind.Should().Be(AuthoredStepKind.Completed);
         run.Status.Should().Be(AuthoredQuestStatus.Completed);
      }

      // The rule that does not move: someone saying it is done moves nothing; only the right person, place or party does.
      [Test]
      public void GIVEN_the_wrong_person_place_or_party_WHEN_reported_THEN_the_stage_does_not_move()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q);
         AuthoredQuestEngine.Accept(run, q, 100);

         AuthoredQuestEngine.ConversationOpened(run, q, "notable_x", "village_elsewhere", 101).Kind.Should().Be(AuthoredStepKind.None);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 101);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 101).Kind.Should().Be(AuthoredStepKind.None);
         run.StageId.Should().Be("question_the_village");
      }

      // Gabriel: no moon, nights of waiting. A patient player is never left waiting past the last night.
      [Test]
      public void GIVEN_nights_of_watch_WHEN_the_last_night_comes_THEN_the_scene_opens_for_certain()
      {
         AuthoredQuest q = Wolf();
         AuthoredStage watch = q.Stages.Single(s => s.Id == "keep_watch");
         watch.Objective.Chance = 0;
         AuthoredQuestRun run = Bound(q);
         run.StageId = "keep_watch";

         var opened = 0;
         for (int day = 1; day <= watch.Objective.Nights[1]; day++)
            if (AuthoredQuestEngine.NightPassed(run, q, true, day).Kind == AuthoredStepKind.OpenScene) opened = day;

         opened.Should().Be(watch.Objective.Nights[1]);
      }

      // A night away from the wood does not count, and the same night is never counted twice.
      [Test]
      public void GIVEN_a_night_away_or_a_night_counted_twice_WHEN_watching_THEN_only_nights_near_the_place_count_once()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q);
         run.StageId = "keep_watch";

         AuthoredQuestEngine.NightPassed(run, q, false, 1);
         AuthoredQuestEngine.NightPassed(run, q, true, 2);
         AuthoredQuestEngine.NightPassed(run, q, true, 2);

         run.NightsWatched.Should().Be(1);
      }

      // A reload replays the same night the same way: the roll is the quest's and the day's, not the session's.
      [Test]
      public void GIVEN_the_same_night_replayed_after_a_reload_WHEN_rolled_THEN_the_outcome_is_the_same()
      {
         AuthoredQuest q = Wolf();
         var outcomes = new List<AuthoredStepKind>();
         for (var i = 0; i < 2; i++)
         {
            AuthoredQuestRun run = AuthoredQuestRun.Deserialize(Bound(q).Serialize());
            run.StageId = "keep_watch";
            run.NightsWatched = q.Stages.Single(s => s.Id == "keep_watch").Objective.Nights[0] - 1;
            outcomes.Add(AuthoredQuestEngine.NightPassed(run, q, true, 205).Kind);
         }

         outcomes.Distinct().Should().HaveCount(1);
      }

      // Gabriel: a clue is missed only in its manner. Closing early still yields the required findings, to be said.
      [Test]
      public void GIVEN_a_scene_closed_before_a_required_finding_WHEN_closed_THEN_the_missed_finding_is_returned_to_be_said()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q);
         run.StageId = "the_tracks";
         AuthoredQuestEngine.Found(run, q, "chains");

         IReadOnlyList<AuthoredFinding> missed = AuthoredQuestEngine.SceneClosed(run, q, 50, out _);

         missed.Select(f => f.Id).Should().Equal("pelt");
      }

      // The model may only reveal what the file declares, each thing once, and spring each hazard once.
      [Test]
      public void GIVEN_findings_and_hazards_reported_WHEN_unknown_or_repeated_THEN_they_are_refused()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q);
         run.StageId = "the_tracks";

         AuthoredQuestEngine.Found(run, q, "a_dragon").Should().BeFalse();
         AuthoredQuestEngine.Found(run, q, "pelt").Should().BeTrue();
         AuthoredQuestEngine.Found(run, q, "pelt").Should().BeFalse();
         AuthoredQuestEngine.Sprung(run, q, "snare").Should().NotBeNull();
         AuthoredQuestEngine.Sprung(run, q, "snare").Should().BeNull();
      }

      // Only the band spawned for the quest counts: beating other looters near the village does not end it.
      [Test]
      public void GIVEN_another_party_beaten_WHEN_hunting_THEN_the_stage_waits_for_the_spawned_one()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q);
         run.StageId = "hunt_the_slavers";
         run.SpawnedPartyId = "cr_slavers_1";

         AuthoredQuestEngine.PartyDefeated(run, q, "looters_9", 10).Kind.Should().Be(AuthoredStepKind.None);
      }

      // A quest past its time, or a band given time to flee, fails; the failure leaves its memory (the engine's part).
      [Test]
      public void GIVEN_too_many_days_WHEN_a_day_passes_THEN_the_quest_fails()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Bound(q, 100);

         AuthoredQuestEngine.Day(run, q, 100 + q.Failure.Days + 1).Kind.Should().Be(AuthoredStepKind.Failed);
         run.Status.Should().Be(AuthoredQuestStatus.Failed);
      }

      // The roles' names reach the player, never the placeholders.
      [Test]
      public void GIVEN_a_line_with_roles_WHEN_filled_THEN_the_names_replace_the_placeholders()
      {
         AuthoredQuestEngine.Fill(Bound(Wolf()), "Keep watch by the wood of {village} for {giver}.")
                            .Should().Be("Keep watch by the wood of Ortongard for Lord Aldric.");
      }
   }
}
