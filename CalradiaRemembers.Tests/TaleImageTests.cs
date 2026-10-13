// Code written by Gabriel Mailhot, 08/10/2026.
// Images in written tales. Gabriel (08/10/2026): "add images to the quests ... shown in a panel to the left of the
// chat ... corresponding to stages ... the stories should then be in a folder, to keep the JSON and the images together";
// then: in the scenes, opening by themselves, images he provides, and the Tale Editor places them on the stages.
// A stage names an image file of its tale's own folder; it opens beside the chat when that stage's scene opens. An image
// named wrongly or missing is said at load, never handed to the engine to fail on.
// If this breaks, a tale's picture never shows, or a bad file name reaches the texture loader.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleImageTests
   {
      private static AuthoredQuest Tale(string image) => AuthoredTales.Load(AuthoredTales.Fork.Replace(
         "\"id\": \"the_wood\", \"journal\": \"Search the wood by {village}.\",",
         "\"id\": \"the_wood\", \"journal\": \"Search the wood by {village}.\", \"image\": \"" + image + "\","));

      // Gabriel (12/10/2026): one queen per realm, each with her picture, for the Troy-inspired tale ("Image dans les
      // contes"). A stage names "queen_{giver_realm}.jpg" and shows the picture of the queen of the realm the tale is
      // played in.
      [Test]
      public void GIVEN_an_image_named_by_the_givers_realm_WHEN_its_stage_opens_THEN_the_picture_of_that_realm_is_shown()
      {
         AuthoredQuest q = Tale("queen_{giver_realm}.jpg");
         AuthoredQuestRun run = AuthoredTales.At(q, "the_wood");
         run.BoundIds["giver_realm"] = "vlandia";

         AuthoredTaleImages.ImageFor(run, q).Should().Be("queen_vlandia.jpg");
      }

      // A realm the tale was not bound to leaves the name unfilled: no file has such a name, so nothing is shown rather
      // than a wrong queen; and a filled name can never climb out of the tale's folder.
      [Test]
      public void GIVEN_a_role_not_bound_or_a_value_with_a_path_WHEN_resolved_THEN_no_picture_and_never_outside_the_folder()
      {
         AuthoredTaleImages.Resolve("queen_{giver_realm}.jpg", new Dictionary<string, string>()).Should().BeNull();
         AuthoredTaleImages.Resolve("queen_{giver_realm}.jpg", new Dictionary<string, string> {["giver_realm"] = "../x"}).Should().BeNull();
         AuthoredTaleImages.Resolve("dusk.jpg", null).Should().Be("dusk.jpg");
      }

      // An author is not told a per-realm picture is missing: each realm's file is looked for when that realm plays it.
      [Test]
      public void GIVEN_an_image_named_by_realm_WHEN_checked_at_load_THEN_its_files_are_not_demanded()
      {
         AuthoredTaleImages.Problems(Tale("queen_{giver_realm}.jpg"), _ => false).Should().BeEmpty();
      }

      // A stage carries its image's file name.
      [Test]
      public void GIVEN_a_stage_with_an_image_WHEN_loaded_THEN_it_keeps_the_file_name()
      {
         JsonConvert.DeserializeObject<AuthoredStage>("{\"id\":\"s\",\"image\":\"tracks_at_dusk.jpg\"}").Image.Should().Be("tracks_at_dusk.jpg");
      }

      // The scene of the stage under way shows that stage's image.
      [Test]
      public void GIVEN_a_scene_stage_with_an_image_WHEN_its_scene_opens_THEN_its_image_is_the_one_shown()
      {
         AuthoredQuest q = Tale("dusk.jpg");

         AuthoredTaleImages.ImageFor(AuthoredTales.At(q, "the_wood"), q).Should().Be("dusk.jpg");
         AuthoredTaleImages.ImageFor(AuthoredTales.At(q, "the_road"), q).Should().BeNull();
      }

      // Gabriel (11/10/2026): "A word with you" with a notable opened the wolf's picture though the conversation was no step
      // of the tale. At the questioning of the village, a villager of that village IS the step; the lord who gave the tale
      // is only concerned by it, and is spoken to without the picture.
      [Test]
      public void GIVEN_the_village_being_questioned_WHEN_a_villager_or_the_giver_is_spoken_to_THEN_only_the_villager_conversation_is_the_step()
      {
         AuthoredQuest wolf = AuthoredTales.Wolf();
         AuthoredQuestRun run = AuthoredTales.At(wolf, "question_the_village");

         AuthoredTaleImages.ConversationIsTheStep(run, wolf, "notable_x", AuthoredTales.Village).Should().BeTrue();
         AuthoredTaleImages.ConversationIsTheStep(run, wolf, AuthoredTales.Giver, null).Should().BeFalse();
         AuthoredTaleImages.ConversationIsTheStep(run, wolf, "notable_y", "village_elsewhere").Should().BeFalse();
      }

      // When the step is going back to the lord, he is the step and a villager is not.
      [Test]
      public void GIVEN_a_step_to_tell_the_lord_WHEN_he_or_a_villager_is_spoken_to_THEN_only_the_lord_is_the_step()
      {
         AuthoredQuest wolf = AuthoredTales.Wolf();
         AuthoredQuestRun run = AuthoredTales.At(wolf, "tell_the_lord");

         AuthoredTaleImages.ConversationIsTheStep(run, wolf, AuthoredTales.Giver, null).Should().BeTrue();
         AuthoredTaleImages.ConversationIsTheStep(run, wolf, "notable_x", AuthoredTales.Village).Should().BeFalse();
      }

      // While the step is a night's watch, no conversation is it: nobody's chat opens on the picture.
      [Test]
      public void GIVEN_a_step_that_is_no_conversation_WHEN_anyone_is_spoken_to_THEN_none_is_the_step()
      {
         AuthoredQuest wolf = AuthoredTales.Wolf();
         AuthoredQuestRun run = AuthoredTales.At(wolf, "keep_watch");

         AuthoredTaleImages.ConversationIsTheStep(run, wolf, AuthoredTales.Giver, null).Should().BeFalse();
         AuthoredTaleImages.ConversationIsTheStep(run, wolf, "notable_x", AuthoredTales.Village).Should().BeFalse();
      }

      // The states of one conversation (the wife tales): the choices are said to the giver, so the conversation with her is
      // the step and her picture shows.
      [Test]
      public void GIVEN_choices_said_to_the_giver_WHEN_she_is_spoken_to_THEN_it_is_the_step()
      {
         // The road's two talking choices made choices by words said to the giver, so no objective names her.
         const string said = "\"said\": {\"to\": \"{giver}\", \"meaning\": \"the player decides\"}";
         AuthoredQuest q = AuthoredTales.Load(AuthoredTales.Fork
            .Replace("\"objective\": { \"type\": \"be_at\", \"place\": \"{village}\" }, \"next\": \"the_wood\"", said + ", \"next\": \"the_wood\"")
            .Replace("\"objective\": { \"type\": \"talk_to\", \"hero\": \"{giver}\" }, \"sets\": \"refused\"", said + ", \"sets\": \"refused\""));
         AuthoredQuestRun run = AuthoredTales.At(q, "the_road");

         AuthoredTaleImages.ConversationIsTheStep(run, q, AuthoredTales.Giver, null).Should().BeTrue();
      }

      // Gabriel (11/10/2026): "plus petite, affichee en haut a gauche de l'ecran pour ne pas masquer le NPC anime". In a
      // conversation the picture fits the small box; in a scene with nobody in it, the large one; never distorted.
      [Test]
      public void GIVEN_a_picture_WHEN_a_character_is_spoken_to_THEN_it_is_small_and_keeps_its_proportions()
      {
         (float cw, float ch) = SceneImageLayout.Fit(1448, 1086, compact: true);
         (float lw, float lh) = SceneImageLayout.Fit(1448, 1086, compact: false);

         cw.Should().BeLessThanOrEqualTo(SceneImageLayout.CompactWidth);
         ch.Should().BeLessThanOrEqualTo(SceneImageLayout.CompactHeight);
         lw.Should().BeGreaterThan(cw * 2);
         (cw / ch).Should().BeApproximately(1448f / 1086f, 0.01f);
      }

      // Gabriel (11/10/2026): "on accepte une image OU une video. Le premier frame du video EST l'image." A .crvid is accepted
      // wherever a picture is, and is known for a video so the game plays it instead of loading it as a still.
      [Test]
      public void GIVEN_a_crvid_named_as_a_stage_picture_WHEN_checked_THEN_it_is_accepted_and_known_for_a_video()
      {
         AuthoredTaleImages.IsAcceptedName("bath.crvid").Should().BeTrue();
         AuthoredTaleImages.IsVideo("Bath.CRVID").Should().BeTrue();
         AuthoredTaleImages.IsVideo("bath.jpg").Should().BeFalse();
         AuthoredTaleImages.Problems(Tale("dusk.crvid"), name => name == "dusk.crvid").Should().BeEmpty();
      }

      // Present, well named: nothing to say.
      [Test]
      public void GIVEN_an_image_present_in_the_tales_folder_WHEN_checked_THEN_no_problem()
      {
         AuthoredTaleImages.Problems(Tale("dusk.jpg"), name => name == "dusk.jpg").Should().BeEmpty();
      }

      // Missing from the folder: the author is told which stage and which file.
      [Test]
      public void GIVEN_an_image_missing_from_the_folder_WHEN_checked_THEN_the_author_is_told()
      {
         IReadOnlyList<string> problems = AuthoredTaleImages.Problems(Tale("dusk.jpg"), _ => false);

         problems.Should().ContainSingle().Which.Should().Contain("the_wood").And.Contain("dusk.jpg");
      }

      // Gabriel (08/10/2026): findings, hazards and the people met carry images too, shown when the narration reveals,
      // springs or brings them in. When one narration does several, the person met shows over the danger, the danger over
      // the finding.
      [Test]
      public void GIVEN_a_narration_that_reveals_springs_and_meets_WHEN_the_picture_is_chosen_THEN_the_person_met_shows()
      {
         AuthoredQuest q = Tale("dusk.jpg");
         AuthoredScene scene = AuthoredQuestEngine.Current(AuthoredTales.At(q, "the_wood"), q).Objective.Scene;
         scene.Findings[0].Image = "lantern.jpg";
         scene.Hazards.Add(new AuthoredHazard {Id = "snare", Image = "snare.jpg"});
         scene.Encounters.Add(new AuthoredEncounter {Id = "boy", Image = "boy.jpg"});
         AuthoredQuestRun run = AuthoredTales.At(q, "the_wood");

         AuthoredTaleImages.ImageForMarkers(run, q, new[] {"lantern"}, new string[0], new string[0]).Should().Be("lantern.jpg");
         AuthoredTaleImages.ImageForMarkers(run, q, new[] {"lantern"}, new[] {"snare"}, new string[0]).Should().Be("snare.jpg");
         AuthoredTaleImages.ImageForMarkers(run, q, new[] {"lantern"}, new[] {"snare"}, new[] {"boy"}).Should().Be("boy.jpg");
         AuthoredTaleImages.ImageForMarkers(run, q, new string[0], new string[0], new string[0]).Should().BeNull();
      }

      // Their images are checked at load like a stage's.
      [Test]
      public void GIVEN_a_finding_image_missing_WHEN_checked_THEN_the_author_is_told_which()
      {
         AuthoredQuest q = Tale("dusk.jpg");
         AuthoredQuestEngine.Current(AuthoredTales.At(q, "the_wood"), q).Objective.Scene.Findings[0].Image = "lantern.jpg";

         AuthoredTaleImages.Problems(q, name => name == "dusk.jpg").Should().ContainSingle().Which.Should().Contain("lantern");
      }

      // A path, a climb out of the folder, or a format the game does not load is refused before any disk is read.
      [Test]
      public void GIVEN_a_path_or_a_format_not_loaded_WHEN_checked_THEN_it_is_refused()
      {
         AuthoredTaleImages.IsAcceptedName("dusk.jpg").Should().BeTrue();
         AuthoredTaleImages.IsAcceptedName("Dusk.PNG").Should().BeTrue();
         AuthoredTaleImages.IsAcceptedName("../other/dusk.jpg").Should().BeFalse();
         AuthoredTaleImages.IsAcceptedName("images/dusk.jpg").Should().BeFalse();
         AuthoredTaleImages.IsAcceptedName("dusk.gif").Should().BeFalse();
         AuthoredTaleImages.IsAcceptedName("../dusk.crvid").Should().BeFalse();
         AuthoredTaleImages.Problems(Tale("../dusk.jpg"), _ => true).Should().ContainSingle();
      }
   }
}
