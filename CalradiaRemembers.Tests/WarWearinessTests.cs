// Code written by Gabriel Mailhot, 30/06/2026.
// Documents companion war-weariness: wounds and captures accumulate a toll, rest eases it, Valor sets how much
// a companion bears before wanting out, and the retirement they ask for depends on whether they hold land.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class WarWearinessTests
    {
        // ── Accrual (the real toll of war) ──────────────────────────────────

        [Test]
        public void GIVEN_a_battle_wound_WHEN_applied_THEN_the_toll_rises_by_one()
        {
            WarWeariness.ApplyWound(0).Should().Be(WarWeariness.WoundWear);
            WarWeariness.ApplyWound(10).Should().Be(11);
        }

        [Test]
        public void GIVEN_a_capture_WHEN_applied_THEN_the_toll_rises_by_ten()
        {
            WarWeariness.ApplyCapture(0).Should().Be(WarWeariness.CaptureWear);
            WarWeariness.ApplyCapture(30).Should().Be(40);
        }

        [Test]
        public void GIVEN_a_score_near_the_ceiling_WHEN_more_toll_lands_THEN_it_clamps_to_max()
        {
            WarWeariness.ApplyCapture(WarWeariness.Max - 3).Should().Be(WarWeariness.Max);
            WarWeariness.Apply(WarWeariness.Max, 50).Should().Be(WarWeariness.Max);
            WarWeariness.Clamp(-5).Should().Be(WarWeariness.Min);
        }

        // ── Rest (recovery over time) ───────────────────────────────────────

        [Test]
        public void GIVEN_rest_WHEN_easing_the_toll_THEN_it_falls_by_the_step_without_going_below_zero()
        {
            WarWeariness.Rest(20, 5).Should().Be(15);
            WarWeariness.Rest(3, 5).Should().Be(0);   // never overshoots below Min
            WarWeariness.Rest(0, 5).Should().Be(0);   // no-op once rested
        }

        [Test]
        public void GIVEN_a_non_positive_rest_step_WHEN_resting_THEN_the_toll_is_unchanged()
        {
            WarWeariness.Rest(20, 0).Should().Be(20);
            WarWeariness.Rest(20, -4).Should().Be(20);
        }

        // ── Valor-scaled tolerance ──────────────────────────────────────────

        [Test]
        public void GIVEN_a_neutral_valor_WHEN_reading_the_tolerance_THEN_it_is_the_base()
        {
            WarWeariness.RetireThreshold(0).Should().Be(40);
        }

        [Test]
        public void GIVEN_a_braver_companion_WHEN_reading_the_tolerance_THEN_they_bear_more_war()
        {
            WarWeariness.RetireThreshold(2).Should().BeGreaterThan(WarWeariness.RetireThreshold(0));
            WarWeariness.RetireThreshold(0).Should().BeGreaterThan(WarWeariness.RetireThreshold(-2));
        }

        [Test]
        public void GIVEN_an_extreme_valor_WHEN_reading_the_tolerance_THEN_it_stays_within_sane_bounds()
        {
            WarWeariness.RetireThreshold(-5).Should().Be(WarWeariness.MinRetireThreshold);
            WarWeariness.RetireThreshold(9).Should().Be(WarWeariness.MaxRetireThreshold);
        }

        // ── Wanting to retire ───────────────────────────────────────────────

        [Test]
        public void GIVEN_the_toll_reaches_the_tolerance_WHEN_checking_THEN_the_companion_wants_out()
        {
            int thr = WarWeariness.RetireThreshold(0);
            WarWeariness.WantsToRetire(thr, 0).Should().BeTrue();
            WarWeariness.WantsToRetire(thr - 1, 0).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_same_toll_WHEN_one_is_braver_THEN_only_the_cautious_one_wants_out()
        {
            int score = WarWeariness.RetireThreshold(0); // enough to spend a neutral companion
            WarWeariness.WantsToRetire(score, 0).Should().BeTrue();
            WarWeariness.WantsToRetire(score, 2).Should().BeFalse(); // the brave veteran bears it still
        }

        // ── Banding (relative to the companion's own tolerance) ─────────────

        [Test]
        public void GIVEN_rising_toll_WHEN_banding_a_neutral_companion_THEN_it_climbs_fresh_to_spent()
        {
            WarWeariness.Band(0, 0).Should().Be(WearinessBand.Fresh);
            WarWeariness.Band(9, 0).Should().Be(WearinessBand.Fresh);       // < 25% of 40
            WarWeariness.Band(10, 0).Should().Be(WearinessBand.Seasoned);   // 25%
            WarWeariness.Band(23, 0).Should().Be(WearinessBand.Seasoned);   // < 60%
            WarWeariness.Band(24, 0).Should().Be(WearinessBand.Weary);      // 60%
            WarWeariness.Band(39, 0).Should().Be(WearinessBand.Weary);      // < 100%
            WarWeariness.Band(40, 0).Should().Be(WearinessBand.Spent);      // at tolerance
        }

        [Test]
        public void GIVEN_the_same_toll_WHEN_valor_differs_THEN_the_band_shifts_with_tolerance()
        {
            // 40 spends a neutral companion, but a brave one is only weary at the same toll.
            WarWeariness.Band(40, 0).Should().Be(WearinessBand.Spent);
            WarWeariness.Band(40, 2).Should().NotBe(WearinessBand.Spent);
        }

        // ── Retirement shape by landedness ──────────────────────────────────

        [Test]
        public void GIVEN_a_landed_companion_WHEN_they_retire_THEN_they_step_back_to_govern()
        {
            WarWeariness.Retirement(isLanded: true).Should().Be(RetirementKind.StepBackToGovern);
        }

        [Test]
        public void GIVEN_a_landless_companion_WHEN_they_retire_THEN_they_leave_your_service()
        {
            WarWeariness.Retirement(isLanded: false).Should().Be(RetirementKind.LeaveService);
        }

        // ── Description ─────────────────────────────────────────────────────

        [Test]
        public void GIVEN_each_band_WHEN_describing_THEN_the_reading_reflects_the_band()
        {
            WarWeariness.Describe(0, 0).Should().Contain("fresh");
            WarWeariness.Describe(40, 0).Should().Contain("worn out");
        }
    }
}
