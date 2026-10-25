// Code written by Gabriel Mailhot, 24/10/2026.
// The Empress's Summons (Gabriel, 24/10/2026): at Hardcore, while the player is Rhagaea's prisoner where she is, at night,
// a lady of her household fetches the player to her bedchamber; open to both sexes, no child (she is in her forties), the
// player stays a prisoner (freeing them "c'est trop visible"), her regard above 0 to send for them; the night is judged by
// her regard ("ca s'est bien passe si Rhagaea a gagne plus de Regards qu'elle en a perdu"), and the memory stays either
// way, warm, neutral or bitter; not unique, ten videos and more, a new one each time. Held in Extras/RhagaeaNight until
// the videos are made. If these fail, the tale no longer loads, sends for a prisoner she despises, lets the model free or
// impregnate, leaves a warm memory after a bad night, repeats a video, or tells the realm the secret.

#region

using System.IO;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RhagaeaNightTaleTests
   {
      private static string Folder => Path.Combine(SourceTree.ModRepo!, "Extras", "RhagaeaNight", "ModuleData", "Quests", "rhagaea_night");
      private static string Pack => Path.Combine(SourceTree.ModRepo!, "Extras", "RhagaeaNight", "ModuleData", "scenarios", "rhagaea_night.json");

      private static AuthoredQuest Tale() => AuthoredTales.Load(File.ReadAllText(Path.Combine(Folder, "rhagaea_night.json")));

      [SetUp]
      public void Reachable()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
      }

      // STAKE: a Hardcore tale only a scenario opens, that comes back, for a lady who holds the player in some regard.
      [Test]
      public void GIVEN_the_tale_WHEN_read_THEN_it_is_hardcore_scenario_opened_repeatable_and_asks_her_regard_above_zero()
      {
         AuthoredQuest q = Tale();

         q.Requires.Content.Should().Be("hardcore");
         q.OnScenario.Should().NotBeNull();
         q.OnMap.Should().BeNull();
         q.Unique.Should().BeFalse();
         q.RepeatCooldownDays.Should().BeGreaterThan(0);
         q.Requires.Giver.MinRegard.Should().Be(1);
         q.Requires.Player.Gender.Should().BeNullOrEmpty("the tale is open to both sexes");
      }

      // STAKE: the night is judged on the close, and her chamber tells the model the player stays a prisoner and cannot leave.
      [Test]
      public void GIVEN_the_chamber_WHEN_read_THEN_the_night_ends_on_the_close_and_says_the_player_stays_held()
      {
         AuthoredStage chamber = Tale().Stages.Single(s => s.Id == "chamber");

         AuthoredQuestEngine.EndsOnClose(chamber.Objective).Should().BeTrue();
         string facts = string.Join(" ", chamber.Facts.Select(f => f.Text));
         facts.Should().Contain("prisoner").And.Contain("cannot escape").And.Contain("will not free");
      }

      // STAKE: ten videos at least, a new one each playing, all in the tale's own folder names.
      [Test]
      public void GIVEN_the_chamber_WHEN_its_pictures_are_listed_THEN_it_holds_a_pool_of_ten_videos()
      {
         AuthoredStage chamber = Tale().Stages.Single(s => s.Id == "chamber");

         chamber.ImagePool.Should().HaveCountGreaterThanOrEqualTo(10);
         chamber.ImagePool.Should().OnlyContain(v => AuthoredTaleImages.IsVideo(v) && AuthoredTaleImages.IsAcceptedName(v));
      }

      // STAKE: a warm memory and a reward only after a good night, a neutral one after an even one, a bitter one after a bad
      // one; and never a child, never a release.
      [Test]
      public void GIVEN_each_way_the_night_went_WHEN_the_dawn_is_carried_out_THEN_only_its_memory_holds_and_nothing_frees_or_impregnates()
      {
         AuthoredQuest q = Tale();
         AuthoredOutcome dawn = q.Endings["dawn"];

         string Memory(params string[] flags)
            => string.Join(" | ", AuthoredQuestEngine.Consequences(new AuthoredQuestRun {Flags = flags.ToList()}, dawn).Where(c => c.Type == "memory").Select(c => c.Text));

         Memory(TaleRegardFlags.Rose).Should().Contain("our secret");
         Memory().Should().Contain("came to little");
         Memory(TaleRegardFlags.Fell).Should().Contain("betrayed");
         AuthoredQuestEngine.Consequences(new AuthoredQuestRun {Flags = {TaleRegardFlags.Rose}}, dawn).Should().Contain(c => c.Type == "regard" && c.Amount > 0);
         q.Endings.Values.Concat(new[] {q.Reward, q.Failure}).SelectMany(o => o?.Consequences ?? new System.Collections.Generic.List<AuthoredConsequence>())
          .Select(c => c.Type).Should().NotContain(new[] {"conception", "elope"});
      }

      // STAKE (Gabriel, 25/10/2026): a player who will not follow the lady ends the tale; Rhagaea is disappointed and
      // remembers it.
      [Test]
      public void GIVEN_the_player_will_not_follow_the_lady_WHEN_the_tale_ends_THEN_she_is_disappointed_and_remembers_it()
      {
         AuthoredQuest q = Tale();
         AuthoredOutcome refused = q.Endings[q.OnScenario.DeclineEnding];

         refused.Regard.Should().BeNegative();
         refused.Memory.Should().Contain("would not come");
      }

      // STAKE: a night that never closes cleanly ends on its own, leaving her a plain memory, and blocks no other tale for long.
      [Test]
      public void GIVEN_a_night_that_never_closes_WHEN_its_days_run_out_THEN_it_fails_with_a_plain_memory()
      {
         AuthoredQuest q = Tale();

         q.Failure.Days.Should().BeInRange(1, 3);
         q.Failure.Memory.Should().NotBeNullOrWhiteSpace();
      }

      // STAKE: the rule answers a night in her captivity where she is, on a chance, opens this tale with her, never seals,
      // and tells the realm nothing.
      [Test]
      public void GIVEN_the_pack_WHEN_read_THEN_its_rule_opens_the_tale_on_a_captive_night_repeatably_and_silently()
      {
         JToken rule = JObject.Parse(File.ReadAllText(Pack))["rules"]!.Single();

         ((string) rule["trigger"]).Should().Be("ON_CAPTIVE_NIGHT");
         rule["guards"]!.ToObject<string[]>().Should().Contain(new[] {"IF_CAPTOR_IS:lord_1_14", "IF_PRESENT:lord_1_14"}).And.Contain(g => g.StartsWith("IF_CHANCE:"));
         rule["consequences"]!.ToObject<string[]>().Should().Equal("OPEN_TALE:rhagaea_night,lord_1_14");
         ((bool?) rule["fireOnce"] ?? false).Should().BeFalse();
         string.IsNullOrEmpty((string) rule["message"]).Should().BeTrue();
         ScenarioRule.Parse((string) rule["id"], (string) rule["trigger"], rule["guards"]!.ToObject<string[]>(), rule["consequences"]!.ToObject<string[]>(), null)
                     .Should().NotBeNull();
      }
   }
}
