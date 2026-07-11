// Code written by Gabriel Mailhot, 11/07/2026.
// Documents that the divorce-demand refusal countdown only starts once the letter has actually been
// delivered: a demand not yet delivered can never time out into a refusal, however long it waits.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DivorceDemandTimeoutPolicyTests
    {
        [Test]
        public void GIVEN_the_clock_has_not_started_WHEN_checking_ClockStarted_THEN_it_is_false()
        {
            DivorceDemandTimeoutPolicy.ClockStarted(DivorceDemandTimeoutPolicy.ClockNotStarted).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_real_start_day_WHEN_checking_ClockStarted_THEN_it_is_true()
        {
            DivorceDemandTimeoutPolicy.ClockStarted(100).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_clock_has_not_started_WHEN_checking_refusal_due_THEN_it_is_never_due_even_far_in_the_future()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(DivorceDemandTimeoutPolicy.ClockNotStarted, 999999, 12).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_clock_started_today_WHEN_the_grace_period_is_twelve_days_THEN_it_is_not_yet_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(100, 100, 12).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_clock_started_eleven_days_ago_WHEN_the_grace_period_is_twelve_days_THEN_it_is_not_yet_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(100, 111, 12).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_clock_started_exactly_twelve_days_ago_WHEN_the_grace_period_is_twelve_days_THEN_it_is_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(100, 112, 12).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_clock_started_twenty_days_ago_WHEN_the_grace_period_is_twelve_days_THEN_it_is_due()
        {
            DivorceDemandTimeoutPolicy.IsRefusalDue(100, 120, 12).Should().BeTrue();
        }
    }
}
