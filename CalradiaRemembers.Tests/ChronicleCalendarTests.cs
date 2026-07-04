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
        [Test]
        public void GIVEN_day_zero_WHEN_computing_the_year_THEN_it_is_the_first_year()
        {
            ChronicleCalendar.YearOf(0).Should().Be(1);
        }

        [Test]
        public void GIVEN_the_last_day_of_the_first_year_WHEN_computing_the_year_THEN_it_is_still_the_first_year()
        {
            ChronicleCalendar.YearOf(ChronicleCalendar.DaysPerYear - 1).Should().Be(1);
        }

        [Test]
        public void GIVEN_the_first_day_of_the_second_year_WHEN_computing_the_year_THEN_it_rolls_over()
        {
            ChronicleCalendar.YearOf(ChronicleCalendar.DaysPerYear).Should().Be(2);
        }

        [Test]
        public void GIVEN_several_years_in_WHEN_computing_the_year_THEN_it_matches_simple_division()
        {
            ChronicleCalendar.YearOf(ChronicleCalendar.DaysPerYear * 5 + 10).Should().Be(6);
        }
    }
}
