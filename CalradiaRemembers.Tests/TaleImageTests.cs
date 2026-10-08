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

      // A path, a climb out of the folder, or a format the game does not load is refused before any disk is read.
      [Test]
      public void GIVEN_a_path_or_a_format_not_loaded_WHEN_checked_THEN_it_is_refused()
      {
         AuthoredTaleImages.IsAcceptedName("dusk.jpg").Should().BeTrue();
         AuthoredTaleImages.IsAcceptedName("Dusk.PNG").Should().BeTrue();
         AuthoredTaleImages.IsAcceptedName("../other/dusk.jpg").Should().BeFalse();
         AuthoredTaleImages.IsAcceptedName("images/dusk.jpg").Should().BeFalse();
         AuthoredTaleImages.IsAcceptedName("dusk.gif").Should().BeFalse();
         AuthoredTaleImages.Problems(Tale("../dusk.jpg"), _ => true).Should().ContainSingle();
      }
   }
}
