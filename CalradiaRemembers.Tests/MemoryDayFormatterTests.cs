// Code written by Gabriel Mailhot, 07/07/2026.
// Pins how a memory event's absolute game day is rendered for the player.
//
// WHY IT MATTERS: per MemoryDayFormatter's own header, the raw day number alone reads as gibberish (a tester
// once misread the day number as something else entirely and guessed at the rest), so the relative hint
// ("today"/"yesterday"/"N days ago") is what actually makes a memory legible. The function is pure specifically
// so the console (cr.memory) and the in-chat memory panel date the same event identically; a wrong boundary
// here means the two surfaces would disagree, or a memory would misreport its own recency to the player.

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
        // The zero-days-ago boundary: "today" reads naturally, not "0 days ago", which would be technically
        // correct but jarring prose in a chat panel.
        [Test]
        public void GIVEN_a_memory_dated_today_WHEN_describing_THEN_it_reads_today()
        {
            MemoryDayFormatter.Describe(100, 100).Should().Be("Day 100 (today)");
        }

        // The one-day-ago boundary gets its own named word too, for the same reason: "1 days ago" would be
        // grammatically wrong prose.
        [Test]
        public void GIVEN_a_memory_dated_yesterday_WHEN_describing_THEN_it_reads_yesterday()
        {
            MemoryDayFormatter.Describe(99, 100).Should().Be("Day 99 (yesterday)");
        }

        // The general case, beyond the two named boundaries: a plain numeric count.
        [Test]
        public void GIVEN_a_memory_several_days_old_WHEN_describing_THEN_it_reads_the_day_count()
        {
            MemoryDayFormatter.Describe(94, 100).Should().Be("Day 94 (6 days ago)");
        }

        // Nothing sensible can be said relative to "now" about a day that has not happened yet, so the
        // relative hint must be omitted rather than print a nonsensical negative "days ago".
        [Test]
        public void GIVEN_a_future_day_WHEN_describing_THEN_only_the_bare_day_is_shown()
        {
            MemoryDayFormatter.Describe(120, 100).Should().Be("Day 120");
        }

        // A non-positive day is not a real calendar day (nothing to date relative to "today" either), so it
        // must degrade to the bare number rather than produce a misleading relative hint.
        [Test]
        public void GIVEN_a_non_positive_day_WHEN_describing_THEN_only_the_bare_day_is_shown()
        {
            MemoryDayFormatter.Describe(0, 100).Should().Be("Day 0");
        }
    }
}
