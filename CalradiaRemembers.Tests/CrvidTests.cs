// Code written by Gabriel Mailhot, 11/10/2026.
// CR's own video format (.crvid: a zip of JPEG frames and a manifest) and the clock that says which frame shows. Gabriel
// (11/10/2026): the game's video player stops in conversations, so CR plays its own; "je jouerais le video 1 seule fois et
// reviendrais au premier frame", with a loop icon to make it start over. If the manifest rules are wrong, a broken file
// reaches the decoder or a good one is refused; if the clock is wrong, a video runs at the wrong speed, never ends, or
// ends on its last frame instead of its first.

#region

using CalradiaRemembers.Logic.Video;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CrvidTests
   {
      private static CrvidManifest Good(int width = 768, int height = 1152, float fps = 30, int frames = 180)
         => new() {Version = 1, Fps = fps, Width = width, Height = height, Frames = frames};

      // STAKE: the size measured in game (768x1152 at 30 Hz, nothing skipped) is the one an author is told to use; it must play.
      [Test]
      public void GIVEN_the_measured_size_at_30_fps_WHEN_checked_THEN_it_plays()
      {
         CrvidRules.Problems(Good()).Should().BeEmpty();
      }

      // STAKE: a landscape clip of the same area plays as well: the cap is on pixels, not on one shape.
      [Test]
      public void GIVEN_a_landscape_frame_of_the_same_area_WHEN_checked_THEN_it_plays()
      {
         CrvidRules.Problems(Good(1152, 768)).Should().BeEmpty();
      }

      // STAKE: a frame larger than what was measured would cost every frame more than the game was shown to bear.
      [Test]
      public void GIVEN_a_frame_larger_than_measured_WHEN_checked_THEN_the_author_is_told()
      {
         CrvidRules.Problems(Good(1080, 1920)).Should().ContainSingle().Which.Should().Contain("1080x1920");
      }

      // STAKE: a version, rate or frame count the player cannot honour is said once, never handed to the decoder.
      [Test]
      public void GIVEN_an_unknown_version_a_zero_rate_and_no_frame_WHEN_checked_THEN_each_is_reported()
      {
         CrvidRules.Problems(new CrvidManifest {Version = 2, Fps = 0, Width = 100, Height = 100, Frames = 0}).Should().HaveCount(3);
      }

      // STAKE: the converter and the reader agree on the frames' names; frame 0 is the first file.
      [Test]
      public void GIVEN_the_first_frame_WHEN_named_THEN_it_is_frames_000001_jpg()
      {
         CrvidRules.FrameEntry(0).Should().Be("frames/000001.jpg");
         CrvidRules.FrameEntry(179).Should().Be("frames/000180.jpg");
      }

      // STAKE: the frame follows real time at the clip's own rate, whatever the game's frame rate.
      [Test]
      public void GIVEN_a_30_fps_clip_WHEN_one_second_has_passed_THEN_frame_30_shows()
      {
         VideoClock.At(1.0, 30, 180, loop: false).Frame.Should().Be(30);
      }

      // STAKE: Gabriel's rule: played once, the video comes back to its first frame at the end and stays there.
      [Test]
      public void GIVEN_a_clip_played_once_WHEN_its_end_has_passed_THEN_it_is_finished_on_its_first_frame()
      {
         VideoMoment m = VideoClock.At(6.5, 30, 180, loop: false);

         m.Finished.Should().BeTrue();
         m.Frame.Should().Be(0);
      }

      // STAKE: looped, it starts over at the end and never finishes.
      [Test]
      public void GIVEN_a_looped_clip_WHEN_its_end_has_passed_THEN_it_starts_over()
      {
         VideoMoment m = VideoClock.At(6.5, 30, 180, loop: true);

         m.Finished.Should().BeFalse();
         m.Frame.Should().Be(15);
      }

      // STAKE: a clip with nothing to play is finished at once, never divides by zero.
      [Test]
      public void GIVEN_no_frame_WHEN_asked_the_moment_THEN_it_is_finished()
      {
         VideoClock.At(1, 30, 0, loop: true).Finished.Should().BeTrue();
      }
   }
}
