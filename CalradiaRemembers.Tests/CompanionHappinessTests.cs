// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the companion-happiness model — clamping, the band thresholds, the "wants to leave" floor, the
// per-driver direction, and the gentle drift back to baseline (recovery without overshoot).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionHappinessTests
   {
      // ── Clamping ───────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_delta_past_the_bounds_WHEN_applied_THEN_the_score_is_clamped()
      {
         //Arrange/Act/Assert
         CompanionHappiness.Apply(95, +20).Should().Be(CompanionHappiness.Max);
         CompanionHappiness.Apply(5, -20).Should().Be(CompanionHappiness.Min);
      }

      // ── Bands ──────────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_scores_at_each_threshold_WHEN_banded_THEN_they_read_as_expected()
      {
         //Arrange/Act/Assert
         CompanionHappiness.Band(80).Should().Be(HappinessBand.Devoted);
         CompanionHappiness.Band(79).Should().Be(HappinessBand.Content);
         CompanionHappiness.Band(50).Should().Be(HappinessBand.Content);
         CompanionHappiness.Band(49).Should().Be(HappinessBand.Restless);
         CompanionHappiness.Band(25).Should().Be(HappinessBand.Restless);
         CompanionHappiness.Band(24).Should().Be(HappinessBand.Disgruntled);
      }

      // ── Leave floor ────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_score_at_or_below_the_floor_WHEN_asked_THEN_they_want_to_leave()
      {
         //Arrange/Act/Assert
         CompanionHappiness.WantsToLeave(CompanionHappiness.LeaveThreshold).Should().BeTrue();
         CompanionHappiness.WantsToLeave(CompanionHappiness.LeaveThreshold + 1).Should().BeFalse();
      }

      // ── Drivers ────────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_welcome_driver_WHEN_applied_THEN_happiness_rises()
      {
         //Arrange/Act/Assert
         CompanionHappiness.ApplyDriver(60, HappinessDriver.SuitedErrand).Should().BeGreaterThan(60);
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Paid).Should().BeGreaterThan(60);
      }

      [Test]
      public void GIVEN_an_unwelcome_driver_WHEN_applied_THEN_happiness_falls()
      {
         //Arrange/Act/Assert
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Captured).Should().BeLessThan(60);
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Idle).Should().BeLessThan(60);
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Mistreated).Should().BeLessThan(60);
      }

      [Test]
      public void GIVEN_being_taken_prisoner_WHEN_compared_to_a_failed_errand_THEN_capture_stings_more()
      {
         //Arrange/Act/Assert — capture is the worst single mission outcome.
         CompanionHappiness.DeltaFor(HappinessDriver.Captured)
            .Should().BeLessThan(CompanionHappiness.DeltaFor(HappinessDriver.ErrandFailed));
      }

      // ── Drift to baseline ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_score_above_baseline_WHEN_it_drifts_THEN_it_eases_down_toward_default()
      {
         //Arrange/Act/Assert
         CompanionHappiness.DriftToBaseline(70, 3).Should().Be(67);
      }

      [Test]
      public void GIVEN_a_score_below_baseline_WHEN_it_drifts_THEN_it_eases_up_toward_default()
      {
         //Arrange/Act/Assert
         CompanionHappiness.DriftToBaseline(40, 3).Should().Be(43);
      }

      [Test]
      public void GIVEN_a_score_near_baseline_WHEN_it_drifts_THEN_it_lands_on_default_without_overshoot()
      {
         //Arrange/Act/Assert — a step larger than the gap stops at Default, never past it.
         CompanionHappiness.DriftToBaseline(CompanionHappiness.Default - 2, 10).Should().Be(CompanionHappiness.Default);
         CompanionHappiness.DriftToBaseline(CompanionHappiness.Default, 10).Should().Be(CompanionHappiness.Default);
      }
   }
}
