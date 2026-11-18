// Code written by Gabriel Mailhot, 18/11/2026.

// ConceptionChancePolicy turns the MCM "Conception Chance (%)" into the roll's probability. Kámen (Discord, 18/11/2026)
// spammed his scenes for a child: a correct impregnation_risk tag conceived only one time in seven, a flat 15% no one could
// change. If the slider is misread, a player who sets 100 still waits, or one who set nothing loses the shipped 15%.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ConceptionChancePolicyTests
   {
      // STAKE: a player who never opens the slider keeps the chance the game always had.
      [Test]
      public void GIVEN_no_readable_setting_WHEN_the_chance_is_read_THEN_it_is_the_shipped_fifteen_percent()
      {
         ConceptionChancePolicy.Chance(null).Should().BeApproximately(0.15f, 1e-6f);
         ConceptionChancePolicy.DefaultPercent.Should().Be(15);
      }

      // STAKE: 100 must make every act able to conceive do so (Kámen's ask), and 0 none; a stray value never goes beyond.
      [Test]
      public void GIVEN_the_slider_at_its_ends_or_beyond_WHEN_read_THEN_it_holds_between_never_and_always()
      {
         ConceptionChancePolicy.Chance(100).Should().Be(1f);
         ConceptionChancePolicy.Chance(0).Should().Be(0f);
         ConceptionChancePolicy.Chance(250).Should().Be(1f);
         ConceptionChancePolicy.Chance(-5).Should().Be(0f);
         ConceptionChancePolicy.Chance(40).Should().BeApproximately(0.40f, 1e-6f);
      }
   }
}
