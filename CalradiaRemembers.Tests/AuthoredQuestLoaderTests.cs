// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1 (docs/design/AUTHORED_QUESTS.md; Gabriel, 03/10/2026: a repertoire of multi-stage
// quests in JSON that players and modders can extend). The loader reads a file and says, in plain words, everything
// wrong with it, so an author never has to guess why a quest never appears. If it is wrong, a broken quest loads and
// strands the player at a stage that can never end, or a good one is refused for nothing.

#region

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
   public sealed class AuthoredQuestLoaderTests
   {
      private const string Minimal = @"{
        ""id"": ""cr_test"", ""title"": ""Test"", ""unique"": true,
        ""binds"": { ""giver"": ""a lord"", ""village"": ""a village"" },
        ""stages"": [
          { ""id"": ""ask"", ""journal"": ""Talk to {giver}."", ""objective"": { ""type"": ""talk_to"", ""hero"": ""{giver}"" },
            ""on_complete"": { ""next"": ""go"" } },
          { ""id"": ""go"", ""journal"": ""Go to {village}."", ""objective"": { ""type"": ""be_at"", ""place"": ""{village}"", ""radius"": 5 },
            ""on_complete"": { ""next"": ""reward"" } }
        ],
        ""reward"": { ""regard"": 5 }
      }";

      // A sound quest loads, with nothing to report.
      [Test]
      public void GIVEN_a_sound_quest_WHEN_loaded_THEN_it_loads_with_no_problem()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(Minimal, "test.json");

         r.Problems.Should().BeEmpty();
         r.Quests.Should().ContainSingle(q => q.Id == "cr_test" && q.Stages.Count == 2);
      }

      // A stage that leads nowhere would strand the player forever: the quest is refused, and the author told why.
      [Test]
      public void GIVEN_a_stage_whose_next_does_not_exist_WHEN_loaded_THEN_the_quest_is_refused_and_named()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(Minimal.Replace(@"""next"": ""go""", @"""next"": ""nowhere"""), "test.json");

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("cr_test") && p.Contains("nowhere"));
      }

      // An objective the engine cannot check would never complete: refused, with the known types listed.
      [Test]
      public void GIVEN_an_unknown_objective_type_WHEN_loaded_THEN_the_quest_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(Minimal.Replace(@"""type"": ""be_at""", @"""type"": ""fly_to"""), "test.json");

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("fly_to") && p.Contains("talk_to"));
      }

      // A placeholder nobody binds would reach the player as "{rival}": the author is told.
      [Test]
      public void GIVEN_a_placeholder_no_role_binds_WHEN_loaded_THEN_it_is_reported()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(Minimal.Replace("Go to {village}.", "Go to {rival}'s hall."), "test.json");

         r.Problems.Should().Contain(p => p.Contains("{rival}"));
      }

      // tashmetu (Nexus, 11/10/2026): a tale had no way to name the player, so its memories said "The player found the ones
      // who burned our old village..." in the giver's own memory panel, and the character could repeat it. The game binds
      // {player} to the player's name for every tale, so it needs no "binds" entry and is never reported as unbound.
      [Test]
      public void GIVEN_a_tale_naming_the_player_without_binding_it_WHEN_loaded_THEN_nothing_is_reported()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(Minimal.Replace("Go to {village}.", "{player}, go to {village}."), "test.json");

         r.Problems.Should().NotContain(p => p.Contains("{player}"));
      }

      // An exploration scene must have something to find, or the stage can never be completed.
      [Test]
      public void GIVEN_a_scene_with_no_required_finding_WHEN_loaded_THEN_the_quest_is_refused()
      {
         string json = Minimal.Replace(@"""objective"": { ""type"": ""be_at"", ""place"": ""{village}"", ""radius"": 5 }",
            @"""objective"": { ""type"": ""explore"", ""scene"": { ""opening"": { ""canvas"": ""Night."", ""facts"": [] }, ""findings"": [ { ""id"": ""a"", ""required"": false, ""text"": ""x"" } ] } }");

         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse(json, "test.json");

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("required finding"));
      }

      // Two quests with one id would be one quest played twice: the second is refused.
      [Test]
      public void GIVEN_two_files_with_the_same_id_WHEN_loaded_together_THEN_the_second_is_refused()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.ParseAll(new[] {("a.json", Minimal), ("b.json", Minimal)});

         r.Quests.Should().HaveCount(1);
         r.Problems.Should().Contain(p => p.Contains("b.json") && p.Contains("cr_test"));
      }

      // Broken JSON is reported, never thrown at the game.
      [Test]
      public void GIVEN_broken_json_WHEN_loaded_THEN_it_is_reported_not_thrown()
      {
         AuthoredQuestLoadResult r = AuthoredQuestLoader.Parse("{ \"id\": ", "broken.json");

         r.Quests.Should().BeEmpty();
         r.Problems.Should().Contain(p => p.Contains("broken.json"));
      }

      // Every quest CR ships loads with nothing to report: what players get is what the loader accepts.
      [Test]
      public void GIVEN_the_quests_CR_ships_WHEN_loaded_THEN_none_has_a_problem()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");

         string dir = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData", "Quests");
         if (!Directory.Exists(dir)) Assert.Ignore("no shipped quests yet");

         // A tale may be a folder of its own, its JSON beside its images (Gabriel, 08/10/2026): one level down, as the game reads.
         List<string> files = Directory.GetFiles(dir, "*.json").Concat(Directory.GetDirectories(dir).SelectMany(d => Directory.GetFiles(d, "*.json"))).ToList();
         AuthoredQuestLoadResult r = AuthoredQuestLoader.ParseAll(files.Select(f => (f, File.ReadAllText(f))));

         r.Problems.Should().BeEmpty();
         r.Quests.Should().NotBeEmpty();
         // Every image a shipped tale names is in its folder.
         foreach (AuthoredQuest q in r.Quests)
            AuthoredTaleImages.Problems(q, name => File.Exists(Path.Combine(Path.GetDirectoryName(q.Source)!, name))).Should().BeEmpty();
      }
   }
}
