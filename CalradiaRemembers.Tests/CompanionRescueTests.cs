// Code written by Gabriel Mailhot, 28/06/2026.
// Protects the companion-rescue odds: no companions means no rescue, the daily chance scales with how many can
// ride, it is capped, and a roll inside the chance triggers the attempt.
//
// WHY IT MATTERS: this is the "Stance with teeth" 3.2c rescue branch. While the player sits captive and is
// being convoyed to the commissioning lord, AmbushCaptivityBehavior.TryRescueRoll calls ShouldAttempt once
// per day; a true result spawns a companion-led party that ENGAGES the captor convoy, and winning that
// fight is what frees the player before the 3.2b reckoning audience. Get RescueChancePercent wrong and the
// player is either rescued too readily (the captivity arc never gets to play out) or effectively never
// (too low a chance leaves them stuck convoyed with no recourse until the lord's audience).

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionRescueTests
   {
      // TryRescueRoll already short-circuits when FreeRescuers() is empty, so this test's real value is
      // defending the pure function itself: a lone captive player with no companions to send must never
      // read as having ANY rescue chance, in case a future caller skips that pre-check.
      [Test]
      public void GIVEN_no_free_companions_WHEN_asked_THEN_no_chance_and_no_attempt()
      {
         CompanionRescuePolicy.RescueChancePercent(0).Should().Be(0);
         CompanionRescuePolicy.ShouldAttempt(0, 0).Should().BeFalse();
      }

      // Pins both the per-companion scaling AND MaxChancePercent (60): a full retinue must still fall
      // short of certainty, so the captivity arc (3.2b's reckoning audience) always stays a live outcome
      // even for a player who travels with a large clan.
      [Test]
      public void GIVEN_more_companions_WHEN_asked_THEN_the_chance_rises_but_is_capped()
      {
         CompanionRescuePolicy.RescueChancePercent(1).Should().Be(CompanionRescuePolicy.PerCompanionPercent);
         CompanionRescuePolicy.RescueChancePercent(2).Should().Be(2 * CompanionRescuePolicy.PerCompanionPercent);
         CompanionRescuePolicy.RescueChancePercent(99).Should().Be(CompanionRescuePolicy.MaxChancePercent);
      }

      // TryRescueRoll feeds this MBRandom.RandomInt(100), a 0-based roll, so the chance window must be
      // [0, chance) to actually deliver the documented percentage: a roll of chance-1 is the last winning
      // value, and this pins that it still counts.
      [Test]
      public void GIVEN_a_roll_inside_the_chance_WHEN_checked_THEN_the_rescue_is_attempted()
      {
         CompanionRescuePolicy.ShouldAttempt(1, 0).Should().BeTrue();
         CompanionRescuePolicy.ShouldAttempt(1, CompanionRescuePolicy.PerCompanionPercent - 1).Should().BeTrue();
      }

      // The far side of that same boundary: a roll equal to the chance itself must fail, or the exclusive
      // upper bound would drift to inclusive and every rescue chance would silently pay out one point
      // higher than RescueChancePercent documents. _rescueRollDay caps this to one roll per day, so a
      // boundary bug here would misfire consistently, not average out.
      [Test]
      public void GIVEN_a_roll_at_or_above_the_chance_WHEN_checked_THEN_no_rescue_this_day()
      {
         CompanionRescuePolicy.ShouldAttempt(1, CompanionRescuePolicy.PerCompanionPercent).Should().BeFalse();
         CompanionRescuePolicy.ShouldAttempt(3, 99).Should().BeFalse();
      }
   }
}
