// Code written by Gabriel Mailhot, 27/10/2026.
// HairColorBands names the player's hair colour on their encyclopedia page and in what every NPC sees of them. Windmolen
// (Nexus, 27/10/2026): two characters with no red hair were both described as red-haired. If this is wrong, a brown-haired
// player is called red again, or a true redhead loses the one feature people would name first.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HairColorBandsTests
   {
      private static string Hair(int r, int g, int b) => HairColorBands.Describe(new HairRgb {R = r, G = g, B = b});

      // STAKE: Windmolen's report. Ordinary browns are warm and saturated; none of them may read as red.
      [TestCase(110, 70, 40)]
      [TestCase(150, 100, 60)]
      [TestCase(90, 60, 35)]
      public void GIVEN_an_ordinary_brown_WHEN_named_THEN_it_is_never_red(int r, int g, int b)
      {
         Hair(r, g, b).Should().NotBe("red").And.NotBe("auburn");
      }

      // STAKE: a true redhead or copper head must still be called red; it is the first thing anyone would name.
      [TestCase(180, 80, 40)]
      [TestCase(200, 110, 60)]
      public void GIVEN_a_true_red_WHEN_named_THEN_it_is_red(int r, int g, int b)
      {
         Hair(r, g, b).Should().Be("red");
      }

      // STAKE: a reddish brown is neither plain brown nor red; auburn is the word people use.
      [Test]
      public void GIVEN_a_reddish_brown_WHEN_named_THEN_it_is_auburn()
      {
         Hair(120, 60, 35).Should().Be("auburn");
      }

      // STAKE: the other bands must not move with the fix: near-black, grey and fair stay as they were.
      [Test]
      public void GIVEN_black_grey_or_fair_hair_WHEN_named_THEN_each_keeps_its_word()
      {
         Hair(30, 25, 20).Should().Be("dark");
         Hair(150, 150, 150).Should().Be("grey");
         Hair(210, 180, 120).Should().Be("fair");
      }
   }
}
