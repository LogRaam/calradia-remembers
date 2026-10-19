// Code written by Gabriel Mailhot, 30/06/2026.
// Documents companion war-weariness: wounds and captures accumulate a toll, rest eases it, Valor sets how much
// a companion bears before wanting out, and the retirement they ask for depends on whether they hold land.
//
// WHY IT MATTERS: this is a SEPARATE axis from companion happiness/contentment (which drifts back to baseline)
// because war-weariness ACCUMULATES and ends in a dignified retirement, not a bitter departure: a veteran can
// be devoted AND worn out. WarWearinessBehavior reads WantsToRetire/Band/Retirement to drive the weekly
// retirement inquiry (landless companion leaves for good, landed one steps back but stays in the clan) and the
// soft-enforcement rule (dragging a stepped-back veteran back to war repeatedly converts weariness into real
// unhappiness). Get the thresholds or Rest math wrong and a companion either retires too readily (cheapens the
// toll of war) or never gets to, however hard they have been fought.

#region

using CalradiaRemembers.Logic.War;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class WarWearinessTests
    {
        // ── Accrual (the real toll of war) ──────────────────────────────────

        // Pins the per-wound toll (WoundWear=1) that WarWearinessBehavior applies on each wounded transition:
        // get this wrong and a companion's weariness climbs faster or slower than the battles they actually fought.
        [Test]
        public void GIVEN_a_battle_wound_WHEN_applied_THEN_the_toll_rises_by_one()
        {
            WarWeariness.ApplyWound(0).Should().Be(WarWeariness.WoundWear);
            WarWeariness.ApplyWound(10).Should().Be(11);
        }

        // A capture is a far graver toll than a wound (CaptureWear=10 vs WoundWear=1): a companion who was
        // dragged off to a dungeon should feel that far more than a single scratch in battle.
        [Test]
        public void GIVEN_a_capture_WHEN_applied_THEN_the_toll_rises_by_ten()
        {
            WarWeariness.ApplyCapture(0).Should().Be(WarWeariness.CaptureWear);
            WarWeariness.ApplyCapture(30).Should().Be(40);
        }

        // Without the [Min, Max] clamp a run of hard campaigning could push the score past 100 or below 0,
        // corrupting the Band/Describe reading and any downstream math that assumes it stays in range.
        [Test]
        public void GIVEN_a_score_near_the_ceiling_WHEN_more_toll_lands_THEN_it_clamps_to_max()
        {
            WarWeariness.ApplyCapture(WarWeariness.Max - 3).Should().Be(WarWeariness.Max);
            WarWeariness.Apply(WarWeariness.Max, 50).Should().Be(WarWeariness.Max);
            WarWeariness.Clamp(-5).Should().Be(WarWeariness.Min);
        }

        // ── Rest (recovery over time) ───────────────────────────────────────

        // Rest is the ROADMAP-decided recovery lever (weekly tick, ~2/week, "not monotonic") that lets light
        // service heal a companion faster than fighting wears them down. It must never overshoot below Min,
        // or a well-rested companion could read as somehow "negatively" fresh.
        [Test]
        public void GIVEN_rest_WHEN_easing_the_toll_THEN_it_falls_by_the_step_without_going_below_zero()
        {
            WarWeariness.Rest(20, 5).Should().Be(15);
            WarWeariness.Rest(3, 5).Should().Be(0);   // never overshoots below Min
            WarWeariness.Rest(0, 5).Should().Be(0);   // no-op once rested
        }

        // Defensive: a caller passing a zero or negative recovery step (misconfigured tuning) must never
        // silently ADD to the toll instead of easing it.
        [Test]
        public void GIVEN_a_non_positive_rest_step_WHEN_resting_THEN_the_toll_is_unchanged()
        {
            WarWeariness.Rest(20, 0).Should().Be(20);
            WarWeariness.Rest(20, -4).Should().Be(20);
        }

        // ── Valor-scaled tolerance ──────────────────────────────────────────

        // Pins BaseRetireThreshold (90) as the reference point every other Valor case is measured against.
        [Test]
        public void GIVEN_a_neutral_valor_WHEN_reading_the_tolerance_THEN_it_is_the_base()
        {
            WarWeariness.RetireThreshold(0).Should().Be(90);
        }

        // The whole point of the design refinement noted in ROADMAP: Valor raises the TOLERANCE rather than
        // slowing per-event accrual (a +1 wound has no room to scale at integer resolution): a brave, hardened
        // companion should retire later than a cautious one for the identical toll suffered.
        [Test]
        public void GIVEN_a_braver_companion_WHEN_reading_the_tolerance_THEN_they_bear_more_war()
        {
            WarWeariness.RetireThreshold(2).Should().BeGreaterThan(WarWeariness.RetireThreshold(0));
            WarWeariness.RetireThreshold(0).Should().BeGreaterThan(WarWeariness.RetireThreshold(-2));
        }

        // MinRetireThreshold/MaxRetireThreshold exist so neither a coward retires almost immediately nor a
        // hero becomes effectively immortal to war-weariness (per the field's own doc comment).
        [Test]
        public void GIVEN_an_extreme_valor_WHEN_reading_the_tolerance_THEN_it_stays_within_sane_bounds()
        {
            WarWeariness.RetireThreshold(-5).Should().Be(WarWeariness.MinRetireThreshold);
            WarWeariness.RetireThreshold(9).Should().Be(WarWeariness.MaxRetireThreshold);
        }

        // ── Wanting to retire ───────────────────────────────────────────────

        // Boundary must be INCLUSIVE (>=), or WarWearinessBehavior's weekly retirement inquiry could skip a
        // companion sitting exactly at their own tolerance, leaving a spent veteran fighting on indefinitely.
        [Test]
        public void GIVEN_the_toll_reaches_the_tolerance_WHEN_checking_THEN_the_companion_wants_out()
        {
            int thr = WarWeariness.RetireThreshold(0);
            WarWeariness.WantsToRetire(thr, 0).Should().BeTrue();
            WarWeariness.WantsToRetire(thr - 1, 0).Should().BeFalse();
        }

        // Confirms the Valor-scaling actually changes who asks to retire, not just the raw threshold number:
        // two companions who fought identically hard should NOT retire in lockstep if one is far braver.
        [Test]
        public void GIVEN_the_same_toll_WHEN_one_is_braver_THEN_only_the_cautious_one_wants_out()
        {
            int score = WarWeariness.RetireThreshold(0); // enough to spend a neutral companion
            WarWeariness.WantsToRetire(score, 0).Should().BeTrue();
            WarWeariness.WantsToRetire(score, 2).Should().BeFalse(); // the brave veteran bears it still
        }

        // ── Banding (relative to the companion's own tolerance) ─────────────

        // Pins the four band cutoffs (25% / 60% / 100% of tolerance) that Describe() and the encyclopedia/prompt
        // reading rely on: a wrong cutoff would misrepresent a companion's true state to the player or the LLM.
        [Test]
        public void GIVEN_rising_toll_WHEN_banding_a_neutral_companion_THEN_it_climbs_fresh_to_spent()
        {
            WarWeariness.Band(0, 0).Should().Be(WearinessBand.Fresh);
            WarWeariness.Band(21, 0).Should().Be(WearinessBand.Fresh);      // < 25% of 90
            WarWeariness.Band(22, 0).Should().Be(WearinessBand.Seasoned);   // 25% of 90
            WarWeariness.Band(53, 0).Should().Be(WearinessBand.Seasoned);   // < 60% of 90
            WarWeariness.Band(54, 0).Should().Be(WearinessBand.Weary);      // 60% of 90
            WarWeariness.Band(89, 0).Should().Be(WearinessBand.Weary);      // < 100%
            WarWeariness.Band(90, 0).Should().Be(WearinessBand.Spent);      // at tolerance
        }

        // The banding is RELATIVE to each companion's own tolerance (per the method's own doc comment), not an
        // absolute score: without this the same raw toll would read as "Spent" for a hero as much as a coward.
        [Test]
        public void GIVEN_the_same_toll_WHEN_valor_differs_THEN_the_band_shifts_with_tolerance()
        {
            // 90 spends a neutral companion, but a brave one is only weary (or seasoned) at the same toll.
            WarWeariness.Band(90, 0).Should().Be(WearinessBand.Spent);
            WarWeariness.Band(90, 2).Should().NotBe(WearinessBand.Spent);
        }

        // ── Retirement shape by landedness ──────────────────────────────────

        // The "invested" gate (ROADMAP: governor OR leads a party) means a landed veteran keeps their fief and
        // clan membership on retirement: get this wrong and WarWearinessBehavior could evict a governor from
        // their own holding instead of just stepping them back from the field.
        [Test]
        public void GIVEN_a_landed_companion_WHEN_they_retire_THEN_they_step_back_to_govern()
        {
            WarWeariness.Retirement(isLanded: true).Should().Be(RetirementKind.StepBackToGovern);
        }

        // A landless companion has nothing to step back TO, so they leave the clan entirely on a warm farewell
        // (RemoveCompanionAction): this is the "dignified retirement, not a bitter departure" the pillar exists for.
        [Test]
        public void GIVEN_a_landless_companion_WHEN_they_retire_THEN_they_leave_your_service()
        {
            WarWeariness.Retirement(isLanded: false).Should().Be(RetirementKind.LeaveService);
        }

        // ── Description ─────────────────────────────────────────────────────

        // Describe() feeds the encyclopedia line and prompt text the player/LLM actually reads: a wording
        // that doesn't track the real band would tell the player a false story about their companion's state.
        [Test]
        public void GIVEN_each_band_WHEN_describing_THEN_the_reading_reflects_the_band()
        {
            WarWeariness.Describe(0, 0).Should().Contain("fresh");
            WarWeariness.Describe(90, 0).Should().Contain("worn out");
        }

        // ── Minimum tenure ───────────────────────────────────────────────────

        // Pins MinServiceDays at 168 (2 Calradian years of 84 days, per ChronicleCalendar.DaysPerYear): the
        // number every tenure check below is measured against.
        [Test]
        public void GIVEN_the_minimum_service_days_WHEN_reading_the_constant_THEN_it_is_two_calradian_years()
        {
            WarWeariness.MinServiceDays.Should().Be(168); // 2 Calradian years of 84 days
        }

        // Boundary must be INCLUSIVE (>=): a companion who has served exactly 168 days has fully served their
        // two years and may be asked to retire from that day, not one tick later.
        [Test]
        public void GIVEN_service_at_exactly_the_minimum_WHEN_checking_tenure_THEN_it_has_been_served_long_enough()
        {
            WarWeariness.HasServedLongEnough(WarWeariness.MinServiceDays).Should().BeTrue();
        }

        // Without a tenure floor, a freshly recruited companion who accrued toll (e.g. captured twice) could ask
        // to retire in their first weeks: the player reported exactly this on 2026-07-25 (a 22-year-old
        // companion with under a year of service asked to retire).
        [Test]
        public void GIVEN_service_one_day_short_of_the_minimum_WHEN_checking_tenure_THEN_it_has_not_been_served_long_enough()
        {
            WarWeariness.HasServedLongEnough(WarWeariness.MinServiceDays - 1).Should().BeFalse();
        }

        // A brand-new recruit (zero days served) is the sharpest case of the same stake: the tenure gate must
        // block them outright, however hard the toll they have already suffered.
        [Test]
        public void GIVEN_a_brand_new_recruit_WHEN_checking_tenure_THEN_it_has_not_been_served_long_enough()
        {
            WarWeariness.HasServedLongEnough(0).Should().BeFalse();
        }
    }
}
