// Code written by Gabriel Mailhot, 04/07/2026.
// Protects the year-grouping math shared by the text chronicle and the illuminated viewer: a drift
// here would split or merge chapters inconsistently between the two rendering paths.
// 07/08/2026: the season / within-year / ordinal helpers now also feed the letter-archive thread
// headers (LetterThreadAssembler). One wrong boundary would misdate a letter in the archive AND the
// chronicle at once, since both read this same calendar.

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

        // The four seasons must partition the 84-day year evenly at 21 days each; a letter header names the
        // season, so a wrong divisor here would file, say, a late-spring letter under "summer" in the archive.
        [Test]
        public void GIVEN_a_day_in_each_quarter_of_the_year_WHEN_naming_the_season_THEN_it_walks_spring_to_winter()
        {
            ChronicleCalendar.SeasonNameOf(0).Should().Be("spring");
            ChronicleCalendar.SeasonNameOf(ChronicleCalendar.DaysPerSeason).Should().Be("summer");
            ChronicleCalendar.SeasonNameOf(ChronicleCalendar.DaysPerSeason * 2).Should().Be("autumn");
            ChronicleCalendar.SeasonNameOf(ChronicleCalendar.DaysPerSeason * 3).Should().Be("winter");
        }

        // The season must reset with each new year, not run off the end of the four-name table: day 84 is the
        // first day of the second year and must read "spring" again, or the archive header would throw or wrap.
        [Test]
        public void GIVEN_the_first_day_of_a_new_year_WHEN_naming_the_season_THEN_it_is_spring_again()
        {
            ChronicleCalendar.SeasonNameOf(ChronicleCalendar.DaysPerYear).Should().Be("spring");
        }

        // The within-year day is 1-based so the very first campaign day reads "day 1", not "day 0"; the archive
        // header prints this number, and a 0 there would look like a bug to the player.
        [Test]
        public void GIVEN_the_first_and_last_days_of_a_year_WHEN_computing_the_day_of_year_THEN_it_runs_1_to_84()
        {
            ChronicleCalendar.DayOfYear(0).Should().Be(1);
            ChronicleCalendar.DayOfYear(ChronicleCalendar.DaysPerYear - 1).Should().Be(ChronicleCalendar.DaysPerYear);
            ChronicleCalendar.DayOfYear(ChronicleCalendar.DaysPerYear).Should().Be(1);
        }

        // The teens are the English-ordinal trap (11th/12th/13th, not 11st/12nd/13rd); the year clause in every
        // chronicle heading and letter header relies on this, so the special-case band is pinned explicitly.
        [Test]
        public void GIVEN_ordinal_edge_numbers_WHEN_formatting_THEN_the_suffixes_are_correct()
        {
            ChronicleCalendar.Ordinal(1).Should().Be("1st");
            ChronicleCalendar.Ordinal(2).Should().Be("2nd");
            ChronicleCalendar.Ordinal(3).Should().Be("3rd");
            ChronicleCalendar.Ordinal(11).Should().Be("11th");
            ChronicleCalendar.Ordinal(12).Should().Be("12th");
            ChronicleCalendar.Ordinal(13).Should().Be("13th");
            ChronicleCalendar.Ordinal(21).Should().Be("21st");
        }

        // bdionysis80 (Nexus, 12/11/2026): "current date Summer 9 1084 while receiving letters with timestamp summer
        // 1085". STAKE: the date a prompt carries must be the one on the player's screen, or characters write a year ahead.
        [Test]
        public void GIVEN_summer_9_of_1084_WHEN_dated_THEN_it_reads_as_the_game_shows_it()
        {
            int day = 1084 * ChronicleCalendar.DaysPerYear + ChronicleCalendar.DaysPerSeason + 8;

            ChronicleCalendar.GameDateOf(day).Should().Be("summer 9, 1084");
        }

        // STAKE: the last day of a year must not already be the next year, nor its first day still the old one.
        [Test]
        public void GIVEN_the_turn_of_a_year_WHEN_dated_THEN_winter_21_is_followed_by_spring_1_of_the_next_year()
        {
            int lastDay = 1085 * ChronicleCalendar.DaysPerYear - 1;

            ChronicleCalendar.GameDateOf(lastDay).Should().Be("winter 21, 1084");
            ChronicleCalendar.GameDateOf(lastDay + 1).Should().Be("spring 1, 1085");
        }
    }
}
