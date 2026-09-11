// Code written by Gabriel Mailhot, 13/07/2026.
// The BALANCE of the contentment loop, simulated week by week. The player already has a realm to run: they must
// not spend their evenings nursing companion morale (design call, 2026-07-13, after SuperGraham339 reported
// companions sliding into discontent no matter what he promised, until he disabled the system outright).
// The promise these tests pin: idleness ALONE can never carry a companion to the door; only a real, repeated,
// unaddressed grievance can, it takes many months, and reuniting/righting it undoes the harm.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionContentmentBalanceTests
    {
        private const int TwoYearsOfWeeks = 104;

        /// <summary>One weekly pass of the contentment loop for an unoccupied companion, with no other grievance.</summary>
        private static int IdleWeek(int score, int baseline)
        {
            score = CompanionHappiness.ApplyDriver(score, HappinessDriver.Idle);

            return CompanionHappiness.DriftToBaseline(new CompanionHappiness.HappinessDrift {Score = score, Step = CompanionHappiness.WeeklyDriftStep, Baseline = baseline});
        }

        /// <summary>A weekly pass for an unoccupied companion who is ALSO kept from their spouse.</summary>
        private static int IdleAndSeparatedWeek(int score, int baseline)
        {
            score = CompanionHappiness.ApplyDriver(score, HappinessDriver.Idle);
            score = CompanionHappiness.ApplyDriver(score, HappinessDriver.KeptFromLovedOne);

            return CompanionHappiness.DriftToBaseline(new CompanionHappiness.HappinessDrift {Score = score, Step = CompanionHappiness.WeeklyDriftStep, Baseline = baseline});
        }

        [Test]
        public void GIVEN_the_tuning_WHEN_compared_THEN_the_weekly_recovery_outweighs_the_ennui_of_idleness()
        {
            // The single rule the whole rebalance rests on: coming home to rest beats sitting idle.
            CompanionHappiness.WeeklyDriftStep
                .Should().BeGreaterThan(-CompanionHappiness.DeltaFor(HappinessDriver.Idle));
        }

        // The flagship guarantee, played out over the long run rather than one week: two full years of pure
        // idleness, with no other grievance, must never once cross WantsToLeave. This is the exact scenario
        // SuperGraham339 reported (idle companions sliding toward the door regardless).
        [Test]
        public void GIVEN_a_merely_idle_companion_WHEN_two_years_pass_THEN_they_rest_at_their_clans_baseline_and_never_want_to_leave()
        {
            int baseline = CompanionBaseline.From(3);
            int score = baseline;

            for (var week = 0; week < TwoYearsOfWeeks; week++)
            {
                score = IdleWeek(score, baseline);
                CompanionHappiness.WantsToLeave(score).Should().BeFalse(); // never, not once, in two years
            }

            score.Should().Be(baseline); // they come to rest exactly where their clan's standing places them
            CompanionHappiness.Band(score).Should().Be(HappinessBand.Content);
        }

        // Same idle scenario against the other end of CompanionBaseline: a tier-6 clan's companion must settle
        // Devoted with ZERO courting, because serving a great house is the honour, not the player's attention.
        [Test]
        public void GIVEN_an_idle_companion_of_a_great_house_WHEN_two_years_pass_THEN_they_rest_DEVOTED()
        {
            int baseline = CompanionBaseline.From(6);
            int score = baseline;

            for (var week = 0; week < TwoYearsOfWeeks; week++) score = IdleWeek(score, baseline);

            CompanionHappiness.Band(score).Should().Be(HappinessBand.Devoted); // it is an honour to serve you
        }

        [Test]
        public void GIVEN_a_companion_who_started_low_WHEN_left_idle_in_a_proud_clan_THEN_they_CLIMB_back_to_contentment()
        {
            // Doing nothing in particular now HEALS a soured companion, instead of finishing them off.
            int baseline = CompanionBaseline.From(5);
            var score = 20; // disgruntled, on the edge of leaving

            for (var week = 0; week < TwoYearsOfWeeks; week++) score = IdleWeek(score, baseline);

            score.Should().Be(baseline); // 78: a proud clan, just short of the devotion a tier-6 house commands
            CompanionHappiness.Band(score).Should().Be(HappinessBand.Content);
        }

        [Test]
        public void GIVEN_a_companion_kept_from_their_spouse_WHEN_it_is_never_addressed_THEN_they_sour_but_only_over_many_months()
        {
            // A REAL grievance still bites, so the system keeps its teeth. It simply takes seasons, not days.
            int baseline = CompanionBaseline.From(3);
            int score = baseline;
            var weeks = 0;

            while (!CompanionHappiness.WantsToLeave(score) && weeks < 500)
            {
                score = IdleAndSeparatedWeek(score, baseline);
                weeks++;
            }

            CompanionHappiness.WantsToLeave(score).Should().BeTrue();
            weeks.Should().BeGreaterThan(20); // many months of ignoring it, not a few days
        }

        [Test]
        public void GIVEN_a_soured_companion_WHEN_the_grievance_is_righted_THEN_they_recover()
        {
            // Reunite them with their spouse and the wound closes: the player's fix actually works.
            int baseline = CompanionBaseline.From(3);
            var score = 16; // one point above the floor, bitter

            for (var week = 0; week < 40; week++) score = IdleWeek(score, baseline);

            CompanionHappiness.WantsToLeave(score).Should().BeFalse();
            CompanionHappiness.Band(score).Should().Be(HappinessBand.Content);
        }

        // A one-off Mistreated hit (a broken promise, a slight) must fade within a handful of weeks of ordinary
        // idle drift, not linger indefinitely: only a REPEATED, unaddressed grievance should carry any weight.
        [Test]
        public void GIVEN_a_single_slight_WHEN_time_passes_THEN_it_is_forgiven_within_weeks()
        {
            int baseline = CompanionBaseline.From(3);
            int score = CompanionHappiness.ApplyDriver(baseline, HappinessDriver.Mistreated);
            score.Should().BeLessThan(baseline);

            for (var week = 0; week < 10; week++) score = IdleWeek(score, baseline);

            score.Should().Be(baseline); // a slight is remembered, not carried forever
        }
    }
}
