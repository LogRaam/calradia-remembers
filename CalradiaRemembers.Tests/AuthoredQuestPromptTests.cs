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
         string file = AuthoredTales.WolfFile;

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

      // Gabriel's play test (04/10/2026): the second villager's conversation completed the stage as it opened, and he spoke
      // with no belief at all ("Bandits. The lair is south-east"). The one whose conversation completes a stage still
      // speaks from that stage while it lasts.
      [Test]
      public void GIVEN_the_villager_whose_conversation_completes_the_questioning_WHEN_he_speaks_THEN_he_still_believes_in_the_beast()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Run(q, "question_the_village");
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_a", Village, 2);
         AuthoredQuestEngine.ConversationOpened(run, q, "notable_b", Village, 2).ToStage.Should().Be("keep_watch");

         AuthoredQuestPrompt.ForCharacter(run, q, "notable_b", Village, offering: false).Should().Contain("werewolf").And.NotContain("slaver");
      }

      // Until the player brings back the truth, the village still believes in the beast: a villager met during the watch
      // must not improvise an answer of his own (the "bandits" of the play test).
      [Test]
      public void GIVEN_the_watch_under_way_WHEN_a_villager_speaks_THEN_he_still_believes_in_the_beast()
      {
         AuthoredQuest q = Wolf();

         AuthoredQuestPrompt.ForCharacter(Run(q, "keep_watch"), q, "notable_c", Village, offering: false).Should().Contain("werewolf").And.NotContain("slaver");
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

      // STAKE (Schmidt, Discord 18/11/2026): "you cannot write that you take out your two handed axe despite not even having
      // one", and "lady Gunjadrid, daughter of Orvi" became an old man of the wood though she was in the nearest town. The
      // narrator is told what the player carries and who in the world they may name.
      [Test]
      public void GIVEN_what_the_player_carries_and_the_lords_nearby_WHEN_the_narrator_is_told_THEN_both_are_in_its_prompt()
      {
         AuthoredQuest q = Wolf();
         var player = new ScenePlayerFacts {
            Carried = new[] {"Sturgian Axe", "Wolf Cloak"},
            Nearby = new[] {new ScenePersonFact {Name = "Gunjadrid of the Gundarid clan", Note = "daughter of Orvi, in Varcheg now"}}
         };

         string prompt = AuthoredQuestPrompt.Narrator(Run(q, "the_tracks"), q, null, player);

         prompt.Should().Contain("WHAT THE PLAYER CARRIES").And.Contain("Sturgian Axe, Wolf Cloak");
         prompt.Should().Contain("Gunjadrid of the Gundarid clan: daughter of Orvi, in Varcheg now");
         prompt.IndexOf("WHAT THE PLAYER CARRIES").Should().BeLessThan(prompt.IndexOf("WHAT IS THERE TO BE FOUND"));
      }

      // fkasad (Nexus, 07/10/2026): following cart tracks on the road, he was told of "the dark wood to your left" four
      // times, the narrator pulling him toward what was to be found. A tale the player can miss must stay missable:
      // the narrator describes what is there, and never herds the player to it.
      [Test]
      public void GIVEN_a_player_going_elsewhere_than_the_findings_WHEN_the_narrator_is_told_THEN_it_is_forbidden_to_steer_them_back()
      {
         AuthoredQuest q = Wolf();
         string prompt = AuthoredQuestPrompt.Narrator(Run(q, "the_tracks"), q);

         prompt.Should().Contain("Never steer the player toward what is to be found");
         prompt.Should().Contain("mention a place or a sign once");
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

      // Someone met in the scene is voiced with what they know, and no longer offered as someone to meet.
      [Test]
      public void GIVEN_the_girl_met_WHEN_the_narrator_is_told_THEN_it_voices_her_with_what_she_knows()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Run(q, "the_tracks");
         AuthoredQuestEngine.Meet(run, q, "escaped_girl").Should().BeTrue();
         AuthoredQuestEngine.Meet(run, q, "escaped_girl").Should().BeFalse();

         string prompt = AuthoredQuestPrompt.Narrator(run, q);

         prompt.Should().Contain("WHO IS WITH THE PLAYER NOW").And.Contain("buyer from the coast").And.NotContain("[[meet:escaped_girl]]");
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

      // STAKE (Schmidt, 18/11/2026): the girl left by the fire in the village was still told as with the player, and walked out
      // of the trees again as a stranger. Once she parts, she is told as met and left behind, never as with the player.
      [Test]
      public void GIVEN_the_girl_met_then_parted_WHEN_the_narrator_is_told_THEN_she_is_known_but_no_longer_with_the_player()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Run(q, "the_tracks");
         AuthoredQuestEngine.Meet(run, q, "escaped_girl");
         AuthoredQuestPrompt.Narrator(run, q).Should().Contain("[[parted:escaped_girl]]");

         AuthoredQuestEngine.Part(run, q, "escaped_girl").Should().BeTrue();
         AuthoredQuestEngine.Part(run, q, "escaped_girl").Should().BeFalse();

         string prompt = AuthoredQuestPrompt.Narrator(run, q);
         prompt.Should().NotContain("WHO IS WITH THE PLAYER NOW").And.Contain("ALREADY MET, AND NO LONGER WITH THE PLAYER").And.Contain("a girl from Ortongard");
         prompt.Should().NotContain("[[meet:escaped_girl]]");
      }

      // STAKE: only someone actually met can part; a marker for a stranger changes nothing.
      [Test]
      public void GIVEN_someone_never_met_WHEN_parted_THEN_nothing_changes()
      {
         AuthoredQuest q = Wolf();
         AuthoredQuestRun run = Run(q, "the_tracks");

         AuthoredQuestEngine.Part(run, q, "escaped_girl").Should().BeFalse();
         AuthoredQuestPrompt.Narrator(run, q).Should().Contain("[[meet:escaped_girl]]");
      }

      // STAKE: the parted marker is the game's, read and taken out of what the player reads.
      [Test]
      public void GIVEN_narration_with_a_parted_marker_WHEN_extracted_THEN_it_is_read_and_removed()
      {
         SceneMarkers m = SceneMarkers.Extract("She stays by the fire. [[parted:escaped_girl]]");

         m.Text.Should().Be("She stays by the fire.");
         m.Parted.Should().Equal("escaped_girl");
      }

      // STAKE (Schmidt): the narrator is told plainly that a finding shown needs its marker in that same reply.
      [Test]
      public void GIVEN_findings_left_WHEN_the_narrator_is_told_THEN_it_learns_the_marker_goes_in_the_same_reply()
      {
         AuthoredQuest q = Wolf();

         AuthoredQuestPrompt.Narrator(Run(q, "the_tracks"), q).Should().Contain("its marker goes in that same reply");
      }
   }
}
