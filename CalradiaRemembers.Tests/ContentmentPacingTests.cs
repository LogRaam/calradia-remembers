// Code written by Gabriel Mailhot, 13/07/2026.
// The player's chosen pacing for companion contentment. The rebalance made the loop deliberately gentle; this
// gives the tension back to those who want it (SuperGraham339 liked the idea, he only wanted it tuned). The rule
// these tests pin above all: WHATEVER the setting, a companion left merely idle can never be carried out of your
// service. The slider tunes how long a REAL grievance takes to fade, never whether idleness alone can kill.

#region

using System;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ContentmentPacingTests
    {
        private static readonly ContentmentPacing[] AllPacings =
            (ContentmentPacing[]) Enum.GetValues(typeof(ContentmentPacing));

        /// <summary>One weekly pass for an unoccupied companion who is ALSO kept from their spouse: a real grievance.</summary>
        private static int GrievingWeek(int score, int baseline, ContentmentPacing pacing)
        {
            score = CompanionHappiness.ApplyDriver(score, HappinessDriver.Idle);
            score = CompanionHappiness.ApplyDriver(score, HappinessDriver.KeptFromLovedOne);

            return CompanionHappiness.DriftToBaseline(score, ContentmentPacingRules.DriftStepFor(pacing), baseline);
        }

        [Test]
        public void GIVEN_ANY_pacing_WHEN_a_companion_is_merely_idle_THEN_the_drift_home_still_outweighs_the_ennui()
        {
            // The guarantee that must survive every setting: idleness alone can never carry anyone to the door.
            foreach (ContentmentPacing pacing in AllPacings)
                ContentmentPacingRules.DriftStepFor(pacing)
                    .Should().BeGreaterThanOrEqualTo(-CompanionHappiness.DeltaFor(HappinessDriver.Idle),
                        $"idleness must never win under {pacing}");
        }

        // The single-tick inequality above played out over the long run, across every pacing including the
        // tightened Exacting one: the core guarantee (idleness alone never reaches WantsToLeave) must survive
        // the whole MCM range, not just the default Balanced setting.
        [Test]
        public void GIVEN_ANY_pacing_WHEN_two_years_of_idleness_pass_THEN_the_companion_never_wants_to_leave()
        {
            foreach (ContentmentPacing pacing in AllPacings)
            {
                int baseline = CompanionBaseline.From(3);
                int score = baseline;
                int step = ContentmentPacingRules.DriftStepFor(pacing);

                for (var week = 0; week < 104; week++)
                {
                    score = CompanionHappiness.ApplyDriver(score, HappinessDriver.Idle);
                    score = CompanionHappiness.DriftToBaseline(score, step, baseline);
                    CompanionHappiness.WantsToLeave(score).Should().BeFalse($"under {pacing}");
                }
            }
        }

        // The slider must actually order Forgiving > Balanced > Exacting in recovery speed, or the MCM option
        // would not mean what its own enum documentation (and the player-facing labels) claim it means.
        [Test]
        public void GIVEN_the_three_pacings_WHEN_compared_THEN_forgiveness_comes_faster_the_gentler_the_setting()
        {
            ContentmentPacingRules.DriftStepFor(ContentmentPacing.Forgiving)
                .Should().BeGreaterThan(ContentmentPacingRules.DriftStepFor(ContentmentPacing.Balanced));
            ContentmentPacingRules.DriftStepFor(ContentmentPacing.Balanced)
                .Should().BeGreaterThan(ContentmentPacingRules.DriftStepFor(ContentmentPacing.Exacting));
        }

        // Balanced must stay wired to CompanionHappiness.WeeklyDriftStep itself, not a separately-tuned copy of
        // the same number, or the two constants could silently drift apart on a future retune.
        [Test]
        public void GIVEN_the_default_WHEN_asked_THEN_it_is_the_balance_the_rebalance_was_tuned_around()
        {
            ContentmentPacingRules.DriftStepFor(ContentmentPacing.Balanced)
                .Should().Be(CompanionHappiness.WeeklyDriftStep);
        }

        [Test]
        public void GIVEN_a_FORGIVING_lord_WHEN_a_real_grievance_is_never_addressed_THEN_they_still_never_leave()
        {
            // Forgiving means exactly that: even a spouse kept from them for years is borne without leaving.
            int baseline = CompanionBaseline.From(3);
            int score = baseline;

            for (var week = 0; week < 200; week++)
            {
                score = GrievingWeek(score, baseline, ContentmentPacing.Forgiving);
                CompanionHappiness.WantsToLeave(score).Should().BeFalse();
            }
        }

        // This is the tension SuperGraham339 said he wanted back: Exacting must give up on an unaddressed real
        // grievance sooner than Balanced, while still taking a bound number of weeks, never a handful of days.
        [Test]
        public void GIVEN_an_EXACTING_lord_WHEN_a_real_grievance_is_never_addressed_THEN_they_leave_SOONER_than_at_balanced()
        {
            int baseline = CompanionBaseline.From(3);

            int WeeksUntilTheyGiveUp(ContentmentPacing pacing)
            {
                int score = baseline;
                var weeks = 0;

                while (!CompanionHappiness.WantsToLeave(score) && weeks < 500)
                {
                    score = GrievingWeek(score, baseline, pacing);
                    weeks++;
                }

                return weeks;
            }

            int exacting = WeeksUntilTheyGiveUp(ContentmentPacing.Exacting);
            int balanced = WeeksUntilTheyGiveUp(ContentmentPacing.Balanced);

            exacting.Should().BeLessThan(balanced); // grudges linger: the tension the tester wanted back
            exacting.Should().BeGreaterThan(10);    // but still seasons, never days
        }
    }
}
