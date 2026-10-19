// Code written by Gabriel Mailhot, 11/08/2026.
// CourtshipTrialBehavior calls CourtshipTrialPolicy.IsComplete on every daily tick to decide whether a marriage
// prospect who has been riding at the player's side has served the trial she set and should now reach out to
// accept the match. These tests pin the STAKE the author's story hangs on: the trial is passed ONLY when the
// prospect is STILL accompanying (never abandoned it part way), is STILL an eligible prospect (no war or
// captivity has since fallen between them), AND the full term has elapsed. If this gate is wrong, a prospect who
// walked away, or one a war has since barred, is treated as having accepted, and the marriage opens on a promise
// she never kept; or a served trial never completes and the whole feature is dead. The term itself must stay at
// two Calradian seasons so the behavior, the self-test, and this file can never drift onto different clocks.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CourtshipTrialPolicyTests
    {
        // The term is two Calradian seasons (21-day season, so 42 days). Pinned so a change to the trial length is
        // a deliberate, visible edit here, not a silent drift that would make every in-game trial complete early or
        // late relative to what the author signed off on.
        [Test]
        public void GIVEN_the_trial_term_WHEN_read_THEN_it_is_two_calradian_seasons()
        {
            CourtshipTrialPolicy.TrialDays.Should().Be(2 * ChronicleCalendar.DaysPerSeason);
            CourtshipTrialPolicy.TrialDays.Should().Be(42);
        }

        // The happy path and the reason the feature exists: a prospect still riding along, still eligible, who has
        // served the full term, has passed. Without this the match never opens and she never reaches out.
        [Test]
        public void GIVEN_still_accompanying_and_eligible_past_the_full_term_WHEN_checked_THEN_the_trial_is_complete()
        {
            CourtshipTrialPolicy.IsComplete(accompanying: true, daysAccompanied: CourtshipTrialPolicy.TrialDays, trialDays: CourtshipTrialPolicy.TrialDays, stillEligibleProspect: true)
                .Should().BeTrue();
        }

        // The exact boundary: the day the term is reached counts as served (>=), so the trial completes on day 42,
        // not day 43. A player who watched the seasons pass would call it broken if it demanded one extra day.
        [Test]
        public void GIVEN_exactly_the_term_has_elapsed_WHEN_checked_THEN_the_trial_is_complete()
        {
            CourtshipTrialPolicy.IsComplete(accompanying: true, daysAccompanied: 42, trialDays: 42, stillEligibleProspect: true)
                .Should().BeTrue();
        }

        // One day short must NOT complete: the boundary sits at the term, and rounding it down would let the match
        // open a day early, before the trial the prospect set was truly served.
        [Test]
        public void GIVEN_one_day_short_of_the_term_WHEN_checked_THEN_the_trial_is_not_complete()
        {
            CourtshipTrialPolicy.IsComplete(accompanying: true, daysAccompanied: 41, trialDays: 42, stillEligibleProspect: true)
                .Should().BeFalse();
        }

        // The lapse guarantee: a prospect who has STOPPED accompanying has abandoned the trial, and the elapsed day
        // count must never carry it to completion on its own. Without this, a prospect who rode along, left, and was
        // never seen again would still "accept" the match once enough days had passed.
        [Test]
        public void GIVEN_no_longer_accompanying_WHEN_checked_THEN_the_trial_is_not_complete_however_long_it_ran()
        {
            CourtshipTrialPolicy.IsComplete(accompanying: false, daysAccompanied: 999, trialDays: 42, stillEligibleProspect: true)
                .Should().BeFalse();
        }

        // The hard-blocker guarantee: if a war or captivity has fallen between them since the trial began, she is no
        // longer an eligible prospect, and the trial must not complete no matter how long she rode along. The match
        // must never open across a barrier the game itself forbids marrying across.
        [Test]
        public void GIVEN_no_longer_an_eligible_prospect_WHEN_checked_THEN_the_trial_is_not_complete()
        {
            CourtshipTrialPolicy.IsComplete(accompanying: true, daysAccompanied: 999, trialDays: 42, stillEligibleProspect: false)
                .Should().BeFalse();
        }

        // Zero days: a trial that only just began is nowhere near served. Guards the start-day edge so a prospect who
        // begins accompanying today is not instantly declared to have passed.
        [Test]
        public void GIVEN_the_trial_only_just_began_WHEN_checked_THEN_it_is_not_complete()
        {
            CourtshipTrialPolicy.IsComplete(accompanying: true, daysAccompanied: 0, trialDays: 42, stillEligibleProspect: true)
                .Should().BeFalse();
        }
    }
}
