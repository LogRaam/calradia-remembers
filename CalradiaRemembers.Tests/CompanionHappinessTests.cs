// Code written by Gabriel Mailhot, 23/06/2026.
// Protects the companion-happiness model — clamping, the band thresholds, the "wants to leave" floor, the
// per-driver direction, and the gentle drift back to baseline (recovery without overshoot).
//
// WHY IT MATTERS: this is the arithmetic the 13/07/2026 rebalance rests on (CHANGELOG v1.30.14; player report,
// SuperGraham339: companions sliding into discontent no matter what he promised, until he disabled the whole
// system). CompanionHappiness.WeeklyDriftStep is deliberately kept LARGER than the Idle driver's penalty, so a
// companion who is merely unoccupied always drifts home to rest instead of being walked to LeaveThreshold by
// idleness alone. Every other companion system (retention, encyclopedia reading, the prompt) reads its verdict
// (Band, WantsToLeave) off this arithmetic, so a wrong clamp, boundary, or drift here propagates everywhere.

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

      // Every other reader (Band, WantsToLeave, the encyclopedia line) assumes a score inside [Min, Max]. Stacking
      // several drivers in one tick without a hard clamp could push the score out of range and break those reads.
      [Test]
      public void GIVEN_a_delta_past_the_bounds_WHEN_applied_THEN_the_score_is_clamped()
      {
         //Arrange/Act/Assert
         CompanionHappiness.Apply(95, +20).Should().Be(CompanionHappiness.Max);
         CompanionHappiness.Apply(5, -20).Should().Be(CompanionHappiness.Min);
      }

      // ── Bands ──────────────────────────────────────────────────────────────

      // Pins the exact threshold values (Devoted=80, Content=50, Restless=25) the encyclopedia line and prompts
      // read off; a one-point shift here silently changes when the player is told a companion is fine vs restless.
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

      // WantsToLeave is what CompanionRetentionPolicy.Decide reads to start the audience/farewell loop. Get the
      // floor wrong and a companion either never gets flagged for the retention loop, or is flagged too early.
      [Test]
      public void GIVEN_a_score_at_or_below_the_floor_WHEN_asked_THEN_they_want_to_leave()
      {
         //Arrange/Act/Assert
         CompanionHappiness.WantsToLeave(CompanionHappiness.LeaveThreshold).Should().BeTrue();
         CompanionHappiness.WantsToLeave(CompanionHappiness.LeaveThreshold + 1).Should().BeFalse();
      }

      // ── Drivers ────────────────────────────────────────────────────────────

      // Basic directionality: a driver named as a welcome one (an errand that fits them, being paid) must actually
      // move the score UP, or the CHANGELOG v1.30.14 promise that ordinary play rewards companions would be inverted.
      [Test]
      public void GIVEN_a_welcome_driver_WHEN_applied_THEN_happiness_rises()
      {
         //Arrange/Act/Assert
         CompanionHappiness.ApplyDriver(60, HappinessDriver.SuitedErrand).Should().BeGreaterThan(60);
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Paid).Should().BeGreaterThan(60);
      }

      // The mirror case: Captured, Idle, and Mistreated must actually move the score DOWN. Idle in particular is
      // the driver the whole rebalance is calibrated against (it must stay weaker than WeeklyDriftStep).
      [Test]
      public void GIVEN_an_unwelcome_driver_WHEN_applied_THEN_happiness_falls()
      {
         //Arrange/Act/Assert
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Captured).Should().BeLessThan(60);
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Idle).Should().BeLessThan(60);
         CompanionHappiness.ApplyDriver(60, HappinessDriver.Mistreated).Should().BeLessThan(60);
      }

      // Relative ordering, not just sign: capture (-12) must sting worse than a merely failed errand (-4), so a
      // future retune of either constant can never accidentally make losing a companion cheaper than losing a task.
      [Test]
      public void GIVEN_being_taken_prisoner_WHEN_compared_to_a_failed_errand_THEN_capture_stings_more()
      {
         //Arrange/Act/Assert — capture is the worst single mission outcome.
         CompanionHappiness.DeltaFor(HappinessDriver.Captured)
            .Should().BeLessThan(CompanionHappiness.DeltaFor(HappinessDriver.ErrandFailed));
      }

      // ── Drift to baseline ──────────────────────────────────────────────────

      // Pins the exact recovery arithmetic (70,3 → 67): this is the weekly "coming home to rest" half of the
      // rebalance, the half that CHANGELOG v1.30.14 relies on to outweigh the ennui of idleness.
      [Test]
      public void GIVEN_a_score_above_baseline_WHEN_it_drifts_THEN_it_eases_down_toward_default()
      {
         //Arrange/Act/Assert
         CompanionHappiness.DriftToBaseline(70, 3).Should().Be(67);
      }

      // The mirror direction: a soured companion also RECOVERS toward baseline, not just a happy one decaying down.
      // This is what lets a righted grievance actually heal a companion instead of leaving them stuck low forever.
      [Test]
      public void GIVEN_a_score_below_baseline_WHEN_it_drifts_THEN_it_eases_up_toward_default()
      {
         //Arrange/Act/Assert
         CompanionHappiness.DriftToBaseline(40, 3).Should().Be(43);
      }

      // Without an overshoot guard, a large weekly step could push a near-baseline score past Default and back,
      // making the happiness meter oscillate/jitter every week instead of settling.
      [Test]
      public void GIVEN_a_score_near_baseline_WHEN_it_drifts_THEN_it_lands_on_default_without_overshoot()
      {
         //Arrange/Act/Assert — a step larger than the gap stops at Default, never past it.
         CompanionHappiness.DriftToBaseline(CompanionHappiness.Default - 2, 10).Should().Be(CompanionHappiness.Default);
         CompanionHappiness.DriftToBaseline(CompanionHappiness.Default, 10).Should().Be(CompanionHappiness.Default);
      }
   }
}
