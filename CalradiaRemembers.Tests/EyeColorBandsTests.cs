// Code written by Gabriel Mailhot, 19/07/2026.
// The words an NPC uses for someone's eye colour, banded from Bannerlord's 0..1 slider offset.
//
// The fault, in play (2026-07-19): the Empress Rhagaea's eyes are amber, and the model wrote "her green
// eyes hold steady on Arwa's grey ones". It was not inventing anything. It was reading our own appearance
// block, and our own table said green. Her measured offset is 0.68, which the old table placed inside
// "deep green" (0.60 to 0.72).
//
// This lived as a private method inside the net472 mod, so NOTHING could test it, and a colour gradient is
// the last place drift should go unwatched: nothing crashes when a table is wrong, the NPC simply lies
// about a face the player is looking at.
//
// THE REFERENCE BELOW IS GABRIEL'S OWN MEASUREMENT: he set the face editor's Eye Color slider to 18 evenly
// spaced positions and photographed each result (offset = n/17, n = 0..17). The tests pin those samples, so
// the table can never again drift away from the thing it claims to describe. Where a boundary sits BETWEEN
// two samples it is a judgement call and is marked TUNE, not fact.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class EyeColorBandsTests
   {
      // THE REPORTED CASE, and the whole reason this file exists. Rhagaea sits at 0.68, one sample past the
      // point where the photographs turn warm. Anything containing "green" here is the shipped bug.
      [Test]
      public void GIVEN_Rhagaeas_measured_offset_WHEN_describing_THEN_her_eyes_are_amber_not_green()
      {
         EyeColorBands.Describe(0.68f).Should().Be("amber");
      }

      // The cool end of Gabriel's series: the slider opens on an ice-pale blue and stays blue through the
      // first quarter of its travel.
      [TestCase(0.000f, "pale blue")]  // n=0,  slider hard left
      [TestCase(0.118f, "blue")]       // n=2
      [TestCase(0.176f, "blue")]       // n=3
      public void GIVEN_the_cool_end_of_the_slider_WHEN_describing_THEN_it_reads_blue(float offset, string expected)
      {
         EyeColorBands.Describe(offset).Should().Be(expected);
      }

      // The old table called this stretch "deep blue". The photographs show it greying, which is a different
      // thing to say about a face and the reason the whole middle was shifted.
      [TestCase(0.235f)] // n=4
      [TestCase(0.294f)] // n=5
      public void GIVEN_the_greying_blues_WHEN_describing_THEN_they_are_not_called_deep_blue(float offset)
      {
         EyeColorBands.Describe(offset).Should().Be("blue-grey");
      }

      // Steel grey, the pivot of the ramp, one sample wide in the series.
      [Test]
      public void GIVEN_the_middle_of_the_slider_WHEN_describing_THEN_it_reads_grey()
      {
         EyeColorBands.Describe(0.353f).Should().Be("grey"); // n=6
      }

      // The greens run from about 0.40 to 0.63 in the photographs, wider and EARLIER than the old table's
      // 0.44 to 0.60, which is the other half of the same drift.
      [TestCase(0.412f)] // n=7
      [TestCase(0.471f)] // n=8
      [TestCase(0.529f)] // n=9
      [TestCase(0.588f)] // n=10
      public void GIVEN_the_green_stretch_of_the_slider_WHEN_describing_THEN_it_reads_green(float offset)
      {
         EyeColorBands.Describe(offset).Should().Contain("green");
      }

      // The warm end, and the correction that matters: by this point the iris is amber and then brown. The
      // old table was still calling 0.71 "deep green" and 0.76 "hazel".
      [TestCase(0.706f, "amber")] // n=12
      [TestCase(0.765f, "brown")] // n=13
      [TestCase(0.824f, "brown")] // n=14
      [TestCase(0.882f, "brown")] // n=15
      public void GIVEN_the_warm_end_of_the_slider_WHEN_describing_THEN_it_reads_amber_then_brown(float offset, string expected)
      {
         EyeColorBands.Describe(offset).Should().Be(expected);
      }

      // The darkest samples: nearly black, and they must not read as an ordinary brown or the darkest eyes
      // in Calradia would be described like everyone else's.
      [TestCase(0.941f)] // n=16
      [TestCase(1.000f)] // n=17, slider hard right
      public void GIVEN_the_darkest_samples_WHEN_describing_THEN_they_read_dark_brown(float offset)
      {
         EyeColorBands.Describe(offset).Should().Be("dark brown");
      }

      // No green may survive anywhere past the point the photographs turn warm. Stated as its own rule
      // because "green" was the word that reached a player, and a single stray band would bring it back.
      [TestCase(0.66f)]
      [TestCase(0.70f)]
      [TestCase(0.80f)]
      [TestCase(0.95f)]
      public void GIVEN_any_offset_past_the_warm_turn_WHEN_describing_THEN_it_never_says_green(float offset)
      {
         EyeColorBands.Describe(offset).Should().NotContain("green");
      }

      // Fail safe: the engine could not supply an offset. Saying nothing about their eyes is correct; a
      // guess is precisely the fault this class exists to prevent.
      [Test]
      public void GIVEN_no_offset_from_the_engine_WHEN_describing_THEN_nothing_is_said()
      {
         EyeColorBands.Describe(-1f).Should().BeNull();
      }

      // Fail safe: an out-of-range offset still yields the darkest word rather than nothing, so a clamped
      // or corrupted value degrades to a plausible face instead of a hole in the description.
      [Test]
      public void GIVEN_an_offset_past_the_end_WHEN_describing_THEN_it_still_yields_a_word()
      {
         EyeColorBands.Describe(1.5f).Should().Be("dark brown");
      }
   }
}
