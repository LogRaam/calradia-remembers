// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests, increment 1: what each character is told, and what the scene's narrator is told
// (docs/design/AUTHORED_QUESTS.md). Gabriel's rulings: a canvas the model rephrases with facts it must keep; a
// character told only what they know at this stage (gating before declaration); characters never lie; the narrator of
// a scene speaks plainly. If these are wrong, a villager blurts the twist before the wood, the lord knows what only
// the player found, a placeholder reaches the screen, or a scene's markers leak into the story.

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
   public sealed class AuthoredQuestPromptTests
   {
      private const string Giver = "lord_ortongard";
      private const string Village = "village_ortongard";

      private static AuthoredQuest Wolf()
      {
         if (!SourceTree.IsReachable) Assert.Ignore($"source tree not reachable: {SourceTree.MissingAnchor}");
         string file = Path.Combine(SourceTree.ModRepo!, "CalradiaRemembers", "ModuleData", "Quests", "wolf_of_the_wood.json");

         return AuthoredQuestLoader.Parse(File.ReadAllText(file), "wolf_of_the_wood.json").Quests.Single();
      }

      private static AuthoredQuestRun Run(AuthoredQuest q, string stage)
      {
         AuthoredQuestRun run = AuthoredQuestEngine.Start(q,
            new Dictionary<string, string> {["giver"] = Giver, ["village"] = Village},
            new Dictionary<string, string> {["giver"] = "Lord Aldric", ["village"] = "Ortongard"}, 1);
         run.StageId = stage;

         return run;
      }

      // The offering: the lord raises the matter in his own words, with the facts he must keep, names filled in.
      [Test]
      public void GIVEN_the_quest_offered_WHEN_the_giver_speaks_THEN_he_has_the_canvas_and_facts_with_names_filled()
      {
         AuthoredQuest q = Wolf();
         string block = AuthoredQuestPrompt.ForCharacter(Run(q, "the_request"), q, Giver, null, offering: true);

         block.Should().Contain("in your own words").And.Contain("Ortongard").And.NotContain("{village}");
         block.Should().Contain("werewolf");
      }

      // Gating before declaration: before the wood, the villagers believe in the beast; nothing tells them the twist.
      [Test]
      public void GIVEN_the_villagers_questioned_WHEN_one_speaks_THEN_they_know_their_belief_and_not_the_twist()
      {
         AuthoredQuest q = Wolf();
         string block = AuthoredQuestPrompt.ForCharacter(Run(q, "question_the_village"), q, "notable_a", Village, offering: false);

         block.Should().Contain("werewolf").And.NotContain("slaver");
      }

      // Someone with no part in the stage hears nothing of the quest.
      [Test]
      public void GIVEN_a_stranger_WHEN_they_speak_THEN_nothing_of_the_quest_is_in_their_prompt()
      {
         AuthoredQuest q = Wolf();

         AuthoredQuestPrompt.ForCharacter(Run(q, "question_the_village"), q, "lord_elsewhere", "town_far", offering: false).Should().BeNull();
      }

      // The lord learns the truth only from the player: in the report conversation he has the stage he is completing.
      [Test]
      public void GIVEN_the_player_reporting_to_the_lord_WHEN_he_speaks_THEN_he_knows_what_the_player_told_him()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Run(q, "tell_the_lord");
         AuthoredQuestEngine.ConversationOpened(run, q, Giver, null, 5);

         AuthoredQuestPrompt.ForCharacter(run, q, Giver, null, offering: false).Should().Contain("slavers");
      }

      // Gabriel: the narrator speaks plainly, and knows what may be found and what danger the player's choices can spring.
      [Test]
      public void GIVEN_the_scene_WHEN_the_narrator_is_told_THEN_it_speaks_plainly_and_knows_findings_hazards_and_markers()
      {
         AuthoredQuest q = Wolf();
         string prompt = AuthoredQuestPrompt.Narrator(Run(q, "the_tracks"), q);

         prompt.Should().Contain("plain").And.Contain("[[found:pelt]]").And.Contain("undergrowth").And.Contain("[[meet:escaped_girl]]");
         prompt.Should().NotContain("{village}");
      }

      // A finding already revealed is no longer offered to be found.
      [Test]
      public void GIVEN_a_finding_already_revealed_WHEN_the_narrator_is_told_THEN_it_is_not_offered_again()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Run(q, "the_tracks");
         AuthoredQuestEngine.Found(run, q, "chains");

         AuthoredQuestPrompt.Narrator(run, q).Should().NotContain("[[found:chains]]");
      }

      // The markers are the game's: the player reads the story without them, the game gets them.
      [Test]
      public void GIVEN_narration_with_markers_WHEN_extracted_THEN_the_text_is_clean_and_the_markers_are_read()
      {
         SceneMarkers m = SceneMarkers.Extract("You part the bracken. [[found:pelt]] A wolf skin on a frame stares back.\n[[hazard:snare]]");

         m.Text.Should().Be("You part the bracken. A wolf skin on a frame stares back.");
         m.Found.Should().Equal("pelt");
         m.Hazards.Should().Equal("snare");
         m.Met.Should().BeEmpty();
      }
   }
}
