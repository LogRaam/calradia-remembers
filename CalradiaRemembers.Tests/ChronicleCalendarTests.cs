// Code written by Gabriel Mailhot, 04/07/2026.
// Protects the year-grouping math shared by the text chronicle and the illuminated viewer: a drift
// here would split or merge chapters inconsistently between the two rendering paths.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChronicleCalendarTests
    {
        // A zero-based year would print "IN THE 0TH YEAR" as the very first heading in every player's
        // chronicle; pins the 1-based year numbering at the earliest possible day.
        [Test]
        public void GIVEN_day_zero_WHEN_computing_the_year_THEN_it_is_the_first_year()
        {
            ChronicleCalendar.YearOf(0).Should().Be(1);
        }

        // Pins the exact roll-over day. Get this one off by one and the first year's chapter either splits
        // in two under the same "1st year" heading or bleeds one day into the second year's chapter.
        [Test]
        public void GIVEN_the_last_day_of_the_first_year_WHEN_computing_the_year_THEN_it_is_still_the_first_year()
        {
            ChronicleCalendar.YearOf(ChronicleCalendar.DaysPerYear - 1).Should().Be(1);
        }

        // The far side of that same boundary. Both ChronicleWriter and ChronicleViewModel call this
        // method to decide when to print a new yearly heading, so a wrong roll-over day here would make
        // the text chronicle and the illuminated viewer agree on the wrong chapter break, not catch each
        // other's mistake.
        [Test]
        public void GIVEN_the_first_day_of_the_second_year_WHEN_computing_the_year_THEN_it_rolls_over()
        {
            ChronicleCalendar.YearOf(ChronicleCalendar.DaysPerYear).Should().Be(2);
        }

        // Guards against a fencepost creeping in for an arbitrary day deep into a campaign, not just the
        // boundary days already pinned above: a long-lived character's chronicle spans many such years.
        [Test]
        public void GIVEN_several_years_in_WHEN_computing_the_year_THEN_it_matches_simple_division()
        {
            ChronicleCalendar.YearOf(ChronicleCalendar.DaysPerYear * 5 + 10).Should().Be(6);
        }
    }
}
