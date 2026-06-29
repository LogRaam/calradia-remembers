// Code written by Gabriel Mailhot, 28/06/2026.
// Protects the companion-rescue odds: no companions means no rescue, the daily chance scales with how many can
// ride, it is capped, and a roll inside the chance triggers the attempt.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionRescueTests
   {
      [Test]
      public void GIVEN_no_free_companions_WHEN_asked_THEN_no_chance_and_no_attempt()
      {
         CompanionRescuePolicy.RescueChancePercent(0).Should().Be(0);
         CompanionRescuePolicy.ShouldAttempt(0, 0).Should().BeFalse();
      }

      [Test]
      public void GIVEN_more_companions_WHEN_asked_THEN_the_chance_rises_but_is_capped()
      {
         CompanionRescuePolicy.RescueChancePercent(1).Should().Be(CompanionRescuePolicy.PerCompanionPercent);
         CompanionRescuePolicy.RescueChancePercent(2).Should().Be(2 * CompanionRescuePolicy.PerCompanionPercent);
         CompanionRescuePolicy.RescueChancePercent(99).Should().Be(CompanionRescuePolicy.MaxChancePercent);
      }

      [Test]
      public void GIVEN_a_roll_inside_the_chance_WHEN_checked_THEN_the_rescue_is_attempted()
      {
         CompanionRescuePolicy.ShouldAttempt(1, 0).Should().BeTrue();
         CompanionRescuePolicy.ShouldAttempt(1, CompanionRescuePolicy.PerCompanionPercent - 1).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_roll_at_or_above_the_chance_WHEN_checked_THEN_no_rescue_this_day()
      {
         CompanionRescuePolicy.ShouldAttempt(1, CompanionRescuePolicy.PerCompanionPercent).Should().BeFalse();
         CompanionRescuePolicy.ShouldAttempt(3, 99).Should().BeFalse();
      }
   }
}
