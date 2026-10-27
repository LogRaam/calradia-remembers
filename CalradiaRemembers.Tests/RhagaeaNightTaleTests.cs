// Code written by Gabriel Mailhot, 24/10/2026.
// The Empress's Summons (Gabriel, 24/10/2026): at Hardcore, while the player is Rhagaea's prisoner where she is, at night,
// a lady of her household fetches the player to her bedchamber; open to both sexes, no child (she is in her forties), the
// player stays a prisoner (freeing them "c'est trop visible"), her regard above 0 to send for them; the night is judged by
// her regard ("ca s'est bien passe si Rhagaea a gagne plus de Regards qu'elle en a perdu"), and the memory stays either
// way, warm, neutral or bitter; not unique, ten videos and more, a new one each time. It lives in its own mod,
// the Calradia Remembers Adult Tales repository beside CR (25/10/2026; its own repository since 27/10/2026), never inside CR. If these fail, the tale no longer loads, sends for
// a prisoner she despises, lets the model free or impregnate, leaves a warm memory after a bad night, repeats a video, or tells the realm the secret.

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
      private static string Folder => Path.Combine(SourceTree.AdultTalesRepo!, "ModuleData", "cr_quests", "rhagaea_night");
      private static string Pack => Path.Combine(SourceTree.AdultTalesRepo!, "ModuleData", "cr_scenarios", "rhagaea_night.json");

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

      // STAKE: Gabriel (25/10/2026): "les 3 premiers servent aux preliminaires, les 3 suivants le NPC prend l'initiative,
      // les 3 suivants il laisse au joueur l'initiative", then a conversation of 5 before dawn; the night is neither too short
      // nor too long, and she asks for the secret in that last part rather than whenever the model guesses the night ends.
      [Test]
      public void GIVEN_the_chamber_WHEN_its_arc_is_read_THEN_it_plays_three_three_three_and_five_and_asks_the_secret_at_the_end()
      {
         AuthoredStage chamber = Tale().Stages.Single(s => s.Id == "chamber");
         AuthoredArc arc = chamber.Objective.Arc;

         arc.Beats.Select(b => b.Exchanges).Should().Equal(3, 3, 3, 5);
         arc.Beats.Select(b => b.Lead).Should().Equal(TaleArc.NpcLeads, TaleArc.NpcLeads, TaleArc.PlayerLeads, TaleArc.NpcLeads);
         TaleArc.FinalId(arc).Should().Be(arc.Beats.Last().Id);
         arc.Beats.Last().Do.Should().Contain("secret");
      }

      // STAKE: "si le NPC a eu un orgasme, le NPC a une conversation avec le joueur de 5 beats puis on ferme le stage vers
      // dawn. Sinon, le NPC reprend le beat ou elle prend l'initiative", with "signaux observes avec tirage en filet": her
      // climax is watched while she leads and while the player leads, the game may decide it after six exchanges of
      // listening, and the loop is capped so the night always reaches dawn.
      [Test]
      public void GIVEN_the_chamber_WHEN_its_arc_is_read_THEN_the_talk_before_dawn_follows_her_climax_and_she_leads_again_otherwise()
      {
         AuthoredArc arc = Tale().Stages.Single(s => s.Id == "chamber").Objective.Arc;
         AuthoredArcBeat yields = arc.Beats.Single(b => b.Id == "yields");

         arc.Signals.Single().Id.Should().Be("npc_climaxed");
         arc.Signals.Single().Net.Should().NotBeNull();
         arc.Beats.Where(b => b.Listens.Contains("npc_climaxed")).Select(b => b.Id).Should().Equal("she_leads", "yields");
         yields.Next.Select(n => (string.Join(",", n.If), n.To)).Should().Equal(("npc_climaxed", "after"), ("", "she_leads"));
         arc.MaxExchanges.Should().BeGreaterThan(0);
      }

      // STAKE: a new video each time the chamber plays, all in the tale's own folder names. Gabriel shot eight (27/10/2026);
      // fewer than that and the night repeats itself within a single visit.
      [Test]
      public void GIVEN_the_chamber_WHEN_its_pictures_are_listed_THEN_it_holds_a_pool_of_eight_videos()
      {
         AuthoredStage chamber = Tale().Stages.Single(s => s.Id == "chamber");

         chamber.ImagePool.Should().HaveCountGreaterThanOrEqualTo(8);
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
         AuthoredChoice stay = q.Stages.Single(s => s.Id == "cell").Choices.Single(c => c.Id == "stay");
         AuthoredOutcome refused = q.Endings[AuthoredQuestLoader.EndingId(stay.Next)];

         stay.Default.Should().BeTrue("closing the scene without following is staying");
         refused.Regard.Should().BeNegative();
         refused.Memory.Should().Contain("would not come");
      }

      // STAKE (Gabriel, 25/10/2026: the summons in role-play, the lady the principal of the scene, with the jailer and the
      // two guards): no popup; the cell is a narrated scene with its video and its four people, whose two ways out are to
      // follow her up to the chamber or to stay.
      [Test]
      public void GIVEN_the_summons_WHEN_read_THEN_it_is_a_scene_at_the_cell_door_with_its_people_and_two_ways_out()
      {
         AuthoredQuest q = Tale();
         AuthoredStage cell = q.Stages.Single(s => s.Id == "cell");

         string.IsNullOrWhiteSpace(q.OnScenario.Invitation).Should().BeTrue("the summons is played, not asked by a popup");
         q.Stages[0].OnComplete.Next.Should().Be("cell");
         cell.Objective.Type.Should().Be("explore");
         cell.Image.Should().Be("woman_cell.crvid");
         File.Exists(Path.Combine(Folder, cell.Image)).Should().BeTrue();
         cell.Objective.Scene.Encounters.Select(e => e.Id).Should().Equal("lady", "jailer", "guards");
         cell.Choices.Select(c => (c.Id, c.Next)).Should().Equal(("follow", "bath"), ("stay", "end:refused"));
      }

      // STAKE (Gabriel, 26/10/2026: "il manque aussi une transition"): the lady takes the prisoner to a small warm bath,
      // washes them and dresses them in clean night clothes, then leads them to the Empress's door, where her task ends
      // and she invites them in; only then does the chamber play. Turning back at either is an ending of its own.
      [Test]
      public void GIVEN_the_prisoner_follows_the_lady_WHEN_read_THEN_the_bath_then_the_door_lead_to_the_chamber()
      {
         AuthoredQuest q = Tale();
         AuthoredStage bath = q.Stages.Single(s => s.Id == "bath");
         AuthoredStage door = q.Stages.Single(s => s.Id == "door");

         bath.Objective.Type.Should().Be("explore");
         bath.Choices.Select(c => (c.Id, c.Next)).Should().Equal(("dressed", "door"), ("back_from_bath", "end:turned_back"));
         door.Objective.Type.Should().Be("explore");
         door.Choices.Select(c => (c.Id, c.Next)).Should().Equal(("enter", "chamber"), ("back_from_door", "end:turned_back"));
         q.Endings.Should().ContainKey("turned_back");
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

      // STAKE (Gabriel, 25/10/2026: "je pourrai l'installer sur mon poste pour le tester avec le jeu sans risquer de
      // l'uploader dans CR"): the pack is a module of its own the launcher lists, loaded after Calradia Remembers, and its
      // tale and rule sit where CR reads another mod's.
      [Test]
      public void GIVEN_the_adult_tales_module_WHEN_read_THEN_it_is_its_own_module_depending_on_CR_with_the_tale_where_CR_reads_it()
      {
         string root = Path.Combine(SourceTree.AdultTalesRepo!);
         string xml = File.ReadAllText(Path.Combine(root, "SubModule.xml"));

         xml.Should().Contain("<Id value=\"CalradiaRemembersAdultTales\" />");
         xml.Should().Contain("<DependedModule Id=\"CalradiaRemembers\"");
         Path.GetFileName(Path.GetDirectoryName(Folder)).Should().Be(AuthoredQuestFolders.ModuleTalesFolder);
         File.Exists(Pack).Should().BeTrue();
      }
   }
}
