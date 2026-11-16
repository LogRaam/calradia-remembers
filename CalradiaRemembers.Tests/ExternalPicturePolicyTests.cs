// Code written by Gabriel Mailhot, 16/11/2026.
// What ExternalPicturePolicy decides: whether a picture another mod hands to CrConversation.ShowPicture appears in the open
// conversation's scene panel (the tales' picture), and how its caption is bounded. VonLemberg (16/11/2026): "Ignore stale
// results after the conversation closes or changes", and "A failed generation must never interrupt the conversation";
// Gabriel: pictures now, videos later. If this is wrong, a picture lands in the wrong conversation, the panel is handed a
// file it cannot draw, or a caption floods the margin.

#region

using CalradiaRemembers.Logic.Extension;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ExternalPicturePolicyTests
   {
      private static ExternalPictureFacts Good(string ext = ".png") => new() {ConversationOpen = true, SessionMatches = true, FileExists = true, Extension = ext};

      // STAKE: the ordinary case: a picture of the open conversation, on disk, is shown.
      [TestCase(".png")]
      [TestCase(".jpg")]
      [TestCase(".JPEG")]
      public void GIVEN_a_picture_of_the_open_conversation_WHEN_asked_THEN_it_is_shown(string ext)
      {
         ExternalPicturePolicy.WhyRefused(Good(ext)).Should().BeNull();
      }

      // STAKE: VonLemberg: "Ignore stale results after the conversation closes or changes": a picture generated for a talk
      // the player has left must not appear in the next one.
      [Test]
      public void GIVEN_a_picture_of_a_closed_or_other_conversation_WHEN_asked_THEN_it_is_not_shown()
      {
         ExternalPicturePolicy.WhyRefused(new ExternalPictureFacts {ConversationOpen = true, SessionMatches = false, FileExists = true, Extension = ".png"}).Should().NotBeNull();
         ExternalPicturePolicy.WhyRefused(new ExternalPictureFacts {ConversationOpen = false, SessionMatches = true, FileExists = true, Extension = ".png"}).Should().NotBeNull();
      }

      // STAKE: Gabriel (16/11/2026): "on verra ca plus tard pour les videos": a video is refused by this door for now.
      [Test]
      public void GIVEN_a_video_WHEN_asked_THEN_it_is_not_shown_yet()
      {
         ExternalPicturePolicy.WhyRefused(Good(".crvid")).Should().Contain(".crvid");
      }

      // STAKE: "Provider errors ... do not crash or block CR": a missing file is refused, never handed to the panel.
      [Test]
      public void GIVEN_a_missing_file_WHEN_asked_THEN_it_is_refused_with_the_reason()
      {
         ExternalPicturePolicy.WhyRefused(new ExternalPictureFacts {ConversationOpen = true, SessionMatches = true, FileExists = false, Extension = ".png"})
            .Should().Contain("does not exist");
      }

      // STAKE: the caption sits under the picture as a title; a paragraph handed in is cut at a word and marked, and kept on one line.
      [Test]
      public void GIVEN_a_long_caption_WHEN_bounded_THEN_it_is_cut_at_a_word_on_one_line()
      {
         string longer = string.Join(" ", System.Linq.Enumerable.Repeat("word", 60)) + "\nsecond line";

         string caption = ExternalPicturePolicy.Caption(longer);

         caption.Length.Should().BeLessThanOrEqualTo(ExternalPicturePolicy.MaxCaptionChars + 3);
         caption.Should().EndWith("...").And.NotContain("\n");
         ExternalPicturePolicy.Caption("  Lucon at the ford  ").Should().Be("Lucon at the ford");
         ExternalPicturePolicy.Caption(null).Should().Be("");
      }
   }
}
