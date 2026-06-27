// Code written by Gabriel Mailhot, 27/06/2026.
// Documents the ingestion-door rules: magnitude is clamped to the carryable range, a summary is bounded to the
// blob-safe length (and an empty one is rejected), and a source is held to a daily quota.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ExternalEventRulesTests
    {
        [Test]
        public void GIVEN_a_negative_magnitude_WHEN_clamping_THEN_it_floors_at_zero()
        {
            ExternalEventRules.ClampMagnitude(-50).Should().Be(0);
        }

        [Test]
        public void GIVEN_an_overlarge_magnitude_WHEN_clamping_THEN_it_caps_at_the_max()
        {
            ExternalEventRules.ClampMagnitude(5000).Should().Be(ExternalEventRules.MaxMagnitude);
        }

        [Test]
        public void GIVEN_a_magnitude_in_range_WHEN_clamping_THEN_it_is_unchanged()
        {
            ExternalEventRules.ClampMagnitude(42).Should().Be(42);
        }

        [Test]
        public void GIVEN_an_empty_or_whitespace_summary_WHEN_bounding_THEN_it_is_rejected_as_null()
        {
            ExternalEventRules.BoundSummary(null).Should().BeNull();
            ExternalEventRules.BoundSummary("   ").Should().BeNull();
        }

        [Test]
        public void GIVEN_an_overlong_summary_WHEN_bounding_THEN_it_is_truncated_to_the_blob_guard()
        {
            string huge = new string('x', ExternalEventRules.MaxSummaryChars + 200);

            ExternalEventRules.BoundSummary(huge).Length.Should().Be(ExternalEventRules.MaxSummaryChars);
        }

        [Test]
        public void GIVEN_a_padded_summary_WHEN_bounding_THEN_it_is_trimmed()
        {
            ExternalEventRules.BoundSummary("  a quiet rumour  ").Should().Be("a quiet rumour");
        }

        [Test]
        public void GIVEN_a_source_at_its_quota_WHEN_checking_THEN_it_is_throttled()
        {
            ExternalEventRules.WithinDailyQuota(ExternalEventRules.MaxPerSourcePerDay - 1).Should().BeTrue();
            ExternalEventRules.WithinDailyQuota(ExternalEventRules.MaxPerSourcePerDay).Should().BeFalse();
        }
    }
}
