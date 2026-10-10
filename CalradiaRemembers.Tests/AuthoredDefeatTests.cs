// Code written by Gabriel Mailhot, 10/10/2026.
// A tale's last fight can be lost (Gabriel, 10/10/2026, after the company-only fight spike: "what happens with the tale
// if the player loses the fight?"). A band's "on_defeat" says what the tale makes of it: an ending of its own
// ("next": "end:<id>"), or, without a next, the tale goes on (a resume: its memories and journal line, and the band
// still there to beat before it flees). Gabriel chose the resume for the Wolf of the Wood. Without "on_defeat", nothing
// changes: the player may try again until the tale's clocks run out. If these rules are wrong, a lost fight ends a tale
// its author meant to go on, a second defeat repeats the village's memory, or a defeat against some other band counts.

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
   public sealed class AuthoredDefeatTests
   {
      private const string Band = "cr_slavers_1";

      private static AuthoredQuest Wolf(string from = null, string to = null)
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string json = File.ReadAllText(AuthoredTales.WolfFile);

         return AuthoredQuestLoader.Parse(from == null ? json : json.Replace(from, to), "wolf_of_the_wood.json").Quests.Single();
      }

      /// <summary>A run of the wolf tale standing at the hunt, its band on the map.</summary>
      private static AuthoredQuestRun AtTheHunt(AuthoredQuest q)
      {
         AuthoredQuestRun run = AuthoredQuestEngine.Start(q, new Dictionary<string, string> {["giver"] = "lord_a", ["village"] = "village_a"},
            new Dictionary<string, string> {["giver"] = "Lord A", ["village"] = "Ortongard"}, 100);
         AuthoredQuestEngine.Accept(run, q, 100);
         run.StageId = "hunt_the_slavers";
         run.StageStartDay = 110;
         run.SpawnedPartyId = Band;

         return run;
      }

      // Gabriel's choice for the wolf: a lost fight is a moment of the tale, not its end. The defeat is told (the village
      // and the lord remember it) and the slavers are still there to be beaten.
      [Test]
      public void GIVEN_the_wolf_tale_WHEN_the_player_loses_to_the_slavers_THEN_the_tale_goes_on_and_the_defeat_is_told()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = AtTheHunt(q);

         AuthoredDefeatResult lost = AuthoredQuestEngine.PlayerLostTo(run, q, Band, 112);

         lost.OnDefeat.Should().NotBeNull();
         lost.Tell.Should().BeTrue();
         lost.Step.Kind.Should().Be(AuthoredStepKind.None);
         run.Status.Should().Be(AuthoredQuestStatus.Active);
         run.StageId.Should().Be("hunt_the_slavers");
         AuthoredQuestEngine.PartyDefeated(run, q, Band, 115).ToStage.Should().Be("return");
      }

      // Losing twice to the same band is told once: the village does not remember the same rout twice.
      [Test]
      public void GIVEN_a_defeat_already_told_WHEN_the_player_loses_again_THEN_it_is_not_told_again()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = AtTheHunt(q);

         AuthoredQuestEngine.PlayerLostTo(run, q, Band, 112).Tell.Should().BeTrue();
         AuthoredQuestEngine.PlayerLostTo(run, q, Band, 114).Tell.Should().BeFalse();
      }

      // An author may end the tale on a defeat: its own ending is reached, as any path's would be.
      [Test]
      public void GIVEN_a_band_whose_defeat_ends_the_tale_WHEN_the_player_loses_THEN_that_ending_is_reached()
      {
         AuthoredQuest q = Wolf();
         q.Stages.Single(s => s.Id == "hunt_the_slavers").Objective.Spawn.OnDefeat.Next = "end:slavers_escape";
         q.Endings["slavers_escape"] = new AuthoredOutcome {Memory = "They got away."};
         AuthoredQuestRun run = AtTheHunt(q);

         AuthoredDefeatResult lost = AuthoredQuestEngine.PlayerLostTo(run, q, Band, 112);

         lost.Step.Kind.Should().Be(AuthoredStepKind.Completed);
         lost.Step.EndingId.Should().Be("slavers_escape");
         run.Status.Should().Be(AuthoredQuestStatus.Completed);
      }

      // Without "on_defeat", a lost fight changes nothing, as before: the player may try again until the clocks run out.
      [Test]
      public void GIVEN_a_band_with_no_on_defeat_WHEN_the_player_loses_THEN_nothing_changes()
      {
         AuthoredQuest q = Wolf();
         q.Stages.Single(s => s.Id == "hunt_the_slavers").Objective.Spawn.OnDefeat = null;
         AuthoredQuestRun run = AtTheHunt(q);

         AuthoredDefeatResult lost = AuthoredQuestEngine.PlayerLostTo(run, q, Band, 112);

         lost.OnDefeat.Should().BeNull();
         lost.Step.Kind.Should().Be(AuthoredStepKind.None);
         run.StageId.Should().Be("hunt_the_slavers");
      }

      // Only the tale's own band counts: losing to some looters on the road is no defeat of the tale.
      [Test]
      public void GIVEN_a_lost_fight_against_another_party_WHEN_reported_THEN_the_tale_ignores_it()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = AtTheHunt(q);

         AuthoredQuestEngine.PlayerLostTo(run, q, "looters_9", 112).OnDefeat.Should().BeNull();
      }

      // A defeat that would end on an ending the tale does not define is refused when the file is read, as any path's.
      [Test]
      public void GIVEN_an_on_defeat_leading_to_an_unknown_ending_WHEN_the_file_is_read_THEN_the_author_is_told()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string json = File.ReadAllText(AuthoredTales.WolfFile).Replace("\"on_defeat\": {", "\"on_defeat\": { \"next\": \"end:nowhere\",");

         AuthoredQuestLoader.Parse(json, "wolf.json").Problems.Should().Contain(p => p.Contains("on_defeat") && p.Contains("nowhere"));
      }

      // A defeat leads to an ending or nowhere (the tale goes on): sending it to a stage is refused, the band would vanish
      // with the stage it belongs to.
      [Test]
      public void GIVEN_an_on_defeat_leading_to_a_stage_WHEN_the_file_is_read_THEN_the_author_is_told()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string json = File.ReadAllText(AuthoredTales.WolfFile).Replace("\"on_defeat\": {", "\"on_defeat\": { \"next\": \"return\",");

         AuthoredQuestLoader.Parse(json, "wolf.json").Problems.Should().Contain(p => p.Contains("on_defeat") && p.Contains("end:"));
      }
   }
}
