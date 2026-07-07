// Code written by Gabriel Mailhot, 07/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MemoryDayFormatterTests
    {
        [Test]
        public void GIVEN_a_memory_dated_today_WHEN_describing_THEN_it_reads_today()
        {
            MemoryDayFormatter.Describe(100, 100).Should().Be("Day 100 (today)");
        }

        [Test]
        public void GIVEN_a_memory_dated_yesterday_WHEN_describing_THEN_it_reads_yesterday()
        {
            MemoryDayFormatter.Describe(99, 100).Should().Be("Day 99 (yesterday)");
        }

        [Test]
        public void GIVEN_a_memory_several_days_old_WHEN_describing_THEN_it_reads_the_day_count()
        {
            MemoryDayFormatter.Describe(94, 100).Should().Be("Day 94 (6 days ago)");
        }

        [Test]
        public void GIVEN_a_future_day_WHEN_describing_THEN_only_the_bare_day_is_shown()
        {
            MemoryDayFormatter.Describe(120, 100).Should().Be("Day 120");
        }

        [Test]
        public void GIVEN_a_non_positive_day_WHEN_describing_THEN_only_the_bare_day_is_shown()
        {
            MemoryDayFormatter.Describe(0, 100).Should().Be("Day 0");
        }
    }
}
