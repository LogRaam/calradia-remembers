// Code written by Gabriel Mailhot, 11/07/2026.
// Documents that the divorce-demand refusal countdown only starts once the letter has actually been
// delivered: a demand not yet delivered can never time out into a refusal, however long it waits.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DivorceDemandTimeoutPolicyTests
    {
        // ClockNotStarted is the sentinel meaning the demand letter has not reached the player yet; if this
        // read true, SweepTimeouts could start comparing against the grace period before delivery ever happened.
        [Test]
        public void GIVEN_the_clock_has_not_started_WHEN_checking_ClockStarted_THEN_it_is_false()
        {
            DivorceDemandTimeoutPolicy.ClockStarted(DivorceDemandTimeoutPolicy.ClockNotStarted).Should().BeFalse();
        }

        // The mirror case: once SweepTimeouts records a real delivery day, the clock must read as running,
        // or the grace period comparison in IsRefusalDue would never actually engage.
        [Test]
        public void GIVEN_a_real_start_day_WHEN_checking_ClockStarted_THEN_it_is_true()
        {
            DivorceDemandTimeoutPolicy.ClockStarted(100).Should().BeTrue();
        }

        // No amount of elapsed time may lapse an undelivered demand into a refusal: the spouse's grudge
        // (registered on timeout, per SpouseDivorceDemandBehavior) must never plant over a letter the
        // player literally never had the chance to answer.
        [Test]
        public void GIVEN_the_clock_has_not_started_WHEN_checking_refusal_due_THEN_it_is_never_due_even_far_in_the_future()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(new DivorceDemandTimeoutPolicy.DemandClockFacts {DemandClockStartDay = DivorceDemandTimeoutPolicy.ClockNotStarted, Today = 999999, GracePeriodDays = 12}).Should().BeFalse();
        }

        // Pins the moment right after delivery: the player must get the full grace window, not zero days,
        // so the very tick delivery is recorded can never also be the tick it times out.
        [Test]
        public void GIVEN_the_clock_started_today_WHEN_the_grace_period_is_twelve_days_THEN_it_is_not_yet_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(new DivorceDemandTimeoutPolicy.DemandClockFacts {DemandClockStartDay = 100, Today = 100, GracePeriodDays = 12}).Should().BeFalse();
        }

        // One day short of the boundary: confirms the comparison is not off-by-one in the lenient
        // direction, which would fire a silent refusal (and its grudge) a day before the grace period is up.
        [Test]
        public void GIVEN_the_clock_started_eleven_days_ago_WHEN_the_grace_period_is_twelve_days_THEN_it_is_not_yet_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(new DivorceDemandTimeoutPolicy.DemandClockFacts {DemandClockStartDay = 100, Today = 111, GracePeriodDays = 12}).Should().BeFalse();
        }

        // The boundary itself must be inclusive: this is the exact day the weekly SweepTimeouts is allowed
        // to register the silent refusal (and its GrudgeSource.Divorce grudge) for a demand nobody answered.
        [Test]
        public void GIVEN_the_clock_started_exactly_twelve_days_ago_WHEN_the_grace_period_is_twelve_days_THEN_it_is_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(new DivorceDemandTimeoutPolicy.DemandClockFacts {DemandClockStartDay = 100, Today = 112, GracePeriodDays = 12}).Should().BeTrue();
        }

        // Well past the boundary, not merely sitting on it: guards against a comparison that only matched
        // equality with the grace period, which would leave a long-overdue demand stuck open forever.
        [Test]
        public void GIVEN_the_clock_started_twenty_days_ago_WHEN_the_grace_period_is_twelve_days_THEN_it_is_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(new DivorceDemandTimeoutPolicy.DemandClockFacts {DemandClockStartDay = 100, Today = 120, GracePeriodDays = 12}).Should().BeTrue();
        }
    }
}
