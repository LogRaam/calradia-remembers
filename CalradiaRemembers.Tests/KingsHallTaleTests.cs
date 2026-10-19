// Code written by Gabriel Mailhot, 18/10/2026.
// The Guest of the King's Hall, the Troy-inspired reference tale of the tale-scenario bridge (docs/design/tale-scenario-
// bridge). Gabriel's decisions (12/10/2026): a queen, never of the player's own realm, asks a guest to take her away; if
// the player does, she rides with them, her husband's realm goes to war over it and everyone knows why, and her return
// is the condition of peace. 18/10/2026: a ruling husband only, the realms whose queen has a face, held back in
// Extras/RealmQueens until he says, and the war stage may block other tales while the war lasts. If these tests fail,
// the tale no longer loads, is offered by the wrong lady, a path strands the player, the war stage cannot end however
// the world turns, or the realm does not answer the elopement with war and the return with peace.

#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Scenarios;
using CalradiaRemembers.Tests.Maps;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class KingsHallTaleTests
   {
      private static string Folder => Path.Combine(SourceTree.ModRepo!, "Extras", "RealmQueens", "ModuleData", "Quests", "the_kings_hall");
      private static string Pack => Path.Combine(SourceTree.ModRepo!, "Extras", "RealmQueens", "ModuleData", "scenarios", "the_kings_hall.json");

      private static AuthoredQuest Tale() => AuthoredTales.Load(File.ReadAllText(Path.Combine(Folder, "the_kings_hall.json")));

      private static AuthoredQuestRun Walked(AuthoredQuest q, params string[] choices)
      {
         AuthoredQuestRun run = AuthoredTales.Start(q);
         run.BoundIds["husband"] = "lord_king";
         run.BoundNames["husband"] = "King Derthert";
         AuthoredQuestEngine.Accept(run, q, 100);
         var day = 101;
         foreach (string c in choices) AuthoredQuestEngine.ForceChoice(run, q, c, day++).Kind.Should().NotBe(AuthoredStepKind.None, $"'{c}' is open");

         return run;
      }

      [SetUp]
      public void Reachable()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
      }

      // STAKE: Gabriel's rulings bound who may offer it: a married queen of another realm who does not lead her house,
      // whose husband rules, in a realm whose queen has her face; and it is a mature tale.
      [Test]
      public void GIVEN_the_tale_WHEN_read_THEN_only_a_queen_of_another_realm_with_a_face_and_a_ruling_husband_offers_it()
      {
         AuthoredQuest q = Tale();

         q.Requires.Giver.Married.Should().BeTrue();
         q.Requires.Giver.NotClanLeader.Should().BeTrue();
         q.Requires.Giver.NotOfPlayersRealm.Should().BeTrue();
         q.Requires.Husband.RulesARealm.Should().BeTrue();
         q.Requires.Content.Should().Be("mature");
         q.Requires.Giver.Realm.Should().BeEquivalentTo("aserai", "vlandia", "sturgia", "empire", "khuzait", "battania");
      }

      // STAKE: every realm the tale may be offered in has the queen's picture beside it; a realm with no picture would
      // show none, which is why Gabriel held the north and the Western Empire back.
      [Test]
      public void GIVEN_each_realm_the_tale_names_WHEN_its_picture_is_looked_for_THEN_it_is_there()
      {
         foreach (string realm in Tale().Requires.Giver.Realm)
            File.Exists(Path.Combine(Folder, $"queen_{realm}.jpg")).Should().BeTrue($"the queen of {realm} has her picture");
      }

      // STAKE: "Invitee et epousable": she leaves with the player, and the war stage carries out the elopement and the
      // news everyone hears, so the scenario declaring war finds her already gone.
      [Test]
      public void GIVEN_the_player_takes_her_away_WHEN_the_war_stage_begins_THEN_she_elopes_and_the_realm_hears_why()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = Walked(q, "meet_again", "take_her");

         run.StageId.Should().Be("the_war");
         AuthoredQuestEngine.OnEnter(run, q).Select(c => c.Type).Should().ContainInOrder("elope", "world_event");
      }

      // STAKE: Gabriel's war stage: her return ends it with her going home; the world may end it otherwise (she or the
      // king dies, the player weds her, the war ends without her); each reaches an ending, none strands the player.
      [Test]
      public void GIVEN_the_war_stage_WHEN_its_ways_out_are_read_THEN_her_return_and_every_end_the_world_brings_are_there()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = Walked(q, "meet_again", "take_her");

         AuthoredQuestEngine.OpenChoices(run, q).Select(c => c.Next).Should()
                            .BeEquivalentTo("end:returned", "end:she_died", "end:he_died", "end:wed", "end:peace_otherwise");
         AuthoredQuestEngine.RolesWatched(run, q).Should().BeEquivalentTo("{giver}", "{husband}");
         q.Endings["returned"].Consequences.Select(c => c.Type).Should().Contain("return_spouse");
      }

      // STAKE: the war is declared only after it is fought: a war stage that has not yet seen its war does not end on
      // "peace" the day it begins, before the scenario's declaration lands.
      [Test]
      public void GIVEN_the_war_stage_on_its_first_day_WHEN_the_realms_are_not_yet_at_war_THEN_the_tale_waits()
      {
         AuthoredQuest q = Tale();
         AuthoredQuestRun run = Walked(q, "meet_again", "take_her");

         AuthoredQuestEngine.WorldNow(run, q, new Dictionary<string, TaleRoleState> {["{giver}"] = new(), ["{husband}"] = new()}, 103)
                            .Kind.Should().Be(AuthoredStepKind.None);
      }

      // STAKE: the honourable ways do not start a war: refusing, telling the king, or letting the feast end each reach
      // their own ending, and only the elopement leads to the war stage.
      [Test]
      public void GIVEN_the_other_answers_WHEN_taken_THEN_each_ends_the_tale_without_the_war()
      {
         AuthoredQuest q = Tale();

         Walked(q, "meet_again", "refuse").EndingId.Should().Be("refused");
         Walked(q, "meet_again", "tell_the_king").EndingId.Should().Be("told");
         Walked(q, "decline_early").EndingId.Should().Be("declined");
         Walked(q, "the_feast_ends").EndingId.Should().Be("no_answer");
      }

      // STAKE: the realm answers: the elopement's stage brings war from a ruling king at peace with the player, her
      // return brings peace, and telling the king earns his gratitude; each rule names the tale's people by role.
      [Test]
      public void GIVEN_the_scenario_pack_WHEN_read_THEN_war_answers_the_elopement_and_peace_answers_her_return()
      {
         var rules = JObject.Parse(File.ReadAllText(Pack))["rules"]!
                            .Select(r => ScenarioRule.Parse((string) r["id"], (string) r["trigger"], r["guards"]?.ToObject<string[]>(),
                                                            r["consequences"]?.ToObject<string[]>(), (string) r["message"], (bool?) r["fireOnce"] ?? false))
                            .ToDictionary(r => r.Id);

         rules["the_kings_hall_war"].Consequences.Select(c => c.Verb + ":" + c.Payload).Should().Equal("DECLARE_WAR:{husband},{player}");
         ScenarioSelector.TriggerMatches(rules["the_kings_hall_war"].Trigger, KeywordToken.Parse("ON_TALE:the_kings_hall:the_war")).Should().BeTrue();
         rules["the_kings_hall_war"].Guards.Select(g => g.Verb + ":" + g.Payload).Should().Contain("IF_RULES:{husband}");

         rules["the_kings_hall_peace"].Consequences.Select(c => c.Verb + ":" + c.Payload).Should().Equal("MAKE_PEACE:{husband},{player}");
         ScenarioSelector.TriggerMatches(rules["the_kings_hall_peace"].Trigger, KeywordToken.Parse("ON_TALE:the_kings_hall:end:returned")).Should().BeTrue();

         ScenarioSelector.TriggerMatches(rules["the_kings_hall_gratitude"].Trigger, KeywordToken.Parse("ON_TALE:the_kings_hall:end:told")).Should().BeTrue();
      }
   }
}
