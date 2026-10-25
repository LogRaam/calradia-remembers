// Code written by Gabriel Mailhot, 11/10/2026.
// The seven wife tales of the Adult Tales module, one per people (Gabriel, 11/10/2026: "Complete-les tous et demain je
// pourrai verifier tout ca et tester"). Each follows the shape of Beneath the Falls: the player finds his wife bathing
// (sometimes watching a while, she knowing it), she joins him, he admires her in states he may pass between and back,
// she invites him (sometimes through further moments, sometimes an afterwards), and the ending leaves her a memory,
// regard and a chance of a child. If these tests fail, a tale no longer loads, asks for the wrong wife, misses a
// picture, strands the player in a state, or cannot be walked to its ending with her speaking from each step.

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
   public sealed class WifeTalesTests
   {
      private static string Tales => Path.Combine(SourceTree.ModRepo!, "Extras", "CalradiaRemembersAdultTales", "ModuleData", "cr_quests");

      private static readonly (string Folder, string Culture)[] Peoples = {
         ("wife_khuzait_falls", "khuzait"), ("wife_aserai_oasis", "aserai"), ("wife_battania_glen", "battania"), ("wife_empire_stream", "empire"),
         ("wife_vlandia_pond", "vlandia"), ("wife_sturgia_falls", "sturgia"), ("wife_nord_fjord", "nord")
      };

      private static IEnumerable<TestCaseData> Each() => Peoples.Select(p => new TestCaseData(p.Folder, p.Culture).SetName("{m}(" + p.Folder + ")"));

      private static AuthoredQuest Tale(string folder) => AuthoredTales.Load(File.ReadAllText(Path.Combine(Tales, folder, folder + ".json")));

      // All seven load together, each under its own id (a repeated id would drop one of them).
      [Test]
      public void GIVEN_the_seven_tales_WHEN_loaded_together_THEN_all_seven_load_without_a_problem()
      {
         AuthoredQuestLoadResult all = AuthoredQuestLoader.ParseAll(Peoples.Select(p => (p.Folder, File.ReadAllText(Path.Combine(Tales, p.Folder, p.Folder + ".json")))));

         all.Problems.Should().BeEmpty();
         all.Quests.Should().HaveCount(7);
      }

      // Gabriel's conditions, the same for every people: his wife of that people, with him, not expecting, on good
      // terms, explicit content, on the map by day in her lands, 15% a day, once.
      [TestCaseSource(nameof(Each))]
      public void GIVEN_a_wife_tale_WHEN_read_THEN_it_asks_for_his_wife_of_that_people_on_the_map(string folder, string culture)
      {
         AuthoredQuest q = Tale(folder);
         GiverRequirement g = q.Requires.Giver;

         (g.Role, g.Gender, g.WithPlayer, g.NotPregnant, g.MinRegard).Should().Be(("spouse", "female", true, true, 10));
         g.Culture.Should().Equal(culture);
         q.Requires.Content.Should().Be("explicit");
         (q.OnMap.StillHours, q.OnMap.ByDay, q.OnMap.InGiversLands, q.OnMap.ChancePerDay).Should().Be((3, true, true, 0.15));
         q.Unique.Should().BeTrue();
         q.Reward.Consequences.Should().ContainSingle(c => c.Type == "conception" && c.Who == "{giver}");
         q.Reward.Memory.Should().NotBeNullOrWhiteSpace();
      }

      // Every stage after the popup shows a picture, and every picture is in the tale's folder.
      [TestCaseSource(nameof(Each))]
      public void GIVEN_a_wife_tale_WHEN_its_pictures_are_checked_THEN_every_stage_has_one_and_it_is_there(string folder, string culture)
      {
         foreach (AuthoredStage s in Tale(folder).Stages.Skip(1))
         {
            s.Image.Should().NotBeNullOrWhiteSpace(s.Id);
            File.Exists(Path.Combine(Tales, folder, s.Image)).Should().BeTrue($"{folder}/{s.Image}");
         }
      }

      // Every way out of every stage is taken by words with her: nothing waits on a place, a day or a fight.
      [TestCaseSource(nameof(Each))]
      public void GIVEN_a_wife_tale_WHEN_its_ways_are_read_THEN_every_one_is_taken_by_words_with_her(string folder, string culture)
      {
         foreach (AuthoredStage s in Tale(folder).Stages.Skip(1))
            s.Choices.Should().OnlyContain(c => c.Said != null && c.Said.To == "{giver}" && c.Objective == null, s.Id);
      }

      // A walk through each, along the shortest way: the popup, every stage on the way with her speaking from it, and the
      // ending reached.
      [TestCaseSource(nameof(Each))]
      public void GIVEN_a_wife_tale_WHEN_walked_to_its_ending_THEN_she_speaks_from_each_stage_and_it_ends_rewarded(string folder, string culture)
      {
         AuthoredQuest q = Tale(folder);
         AuthoredQuestRun run = AuthoredTales.Start(q);
         AuthoredQuestEngine.Accept(run, q, 100);

         foreach (string choice in ShortestWay(q, run.StageId))
         {
            AuthoredStage here = q.Stages.Single(s => s.Id == run.StageId);
            string canvas = here.Canvas.First().Text;
            (AuthoredQuestPrompt.ForCharacter(run, q, AuthoredTales.Giver, null, offering: false) ?? "").Should().Contain(canvas.Substring(0, 30), here.Id);
            AuthoredQuestEngine.Said(run, q, AuthoredTales.Giver, choice, 100).Kind.Should().NotBe(AuthoredStepKind.None, $"{here.Id}/{choice}");
         }

         run.Status.Should().Be(AuthoredQuestStatus.Completed);
      }

      /// <summary>The choice ids of the shortest way from a stage to the reward.</summary>
      private static List<string> ShortestWay(AuthoredQuest q, string from)
      {
         var back = new Dictionary<string, (string Stage, string Choice)>();
         var queue = new Queue<string>(new[] {from});
         var seen = new HashSet<string> {from};
         while (queue.Count > 0)
         {
            string at = queue.Dequeue();
            foreach (AuthoredChoice c in q.Stages.Single(s => s.Id == at).Choices)
            {
               if (c.Next == "reward")
               {
                  var way = new List<string> {c.Id};
                  for (string s = at; s != from; s = back[s].Stage) way.Insert(0, back[s].Choice);

                  return way;
               }

               if (seen.Add(c.Next))
               {
                  back[c.Next] = (at, c.Id);
                  queue.Enqueue(c.Next);
               }
            }
         }

         throw new AssertionException($"no way from {from} to the reward");
      }
   }
}
