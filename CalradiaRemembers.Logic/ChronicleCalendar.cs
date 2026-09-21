// Code written by Gabriel Mailhot, 04/07/2026.
// The player chronicle groups a long life into yearly chapters. Pure date math, shared by the text
// writer (CalradiaRemembers.WorldEvents.ChronicleWriter) and the in-game viewer
// (CalradiaRemembers.Chronicle.ChronicleViewModel) so the two rendering paths never drift on what
// "year N" means. Calradia's calendar has no leap years or irregular months in this mod's model.
// 07/08/2026: season / ordinal / within-year math moved here so the letter-archive thread headers
// (LetterThreadAssembler) render dates from the SAME calendar the chronicle uses.

using NpcMemoryService.Core.Calendar;

namespace CalradiaRemembers.Logic
{
    /// <summary>The shared calendar constants and year math for the player chronicle and letter archive.</summary>
    public static class ChronicleCalendar
    {
        // ONE HOME since 21/09/2026, and it had to be the SDK: the prompt builder restated the calendar in its own
        // literals precisely because it cannot reference Logic, while Logic can reference it. Every caller of these
        // two names keeps working; they simply stopped being a second copy.

        /// <summary>In-game days in one Calradian year, as used throughout the chronicle.</summary>
        public const int DaysPerYear = CalradianCalendar.DaysPerYear;

        /// <summary>In-game days in one Calradian season (four seasons make a year).</summary>
        public const int DaysPerSeason = CalradianCalendar.DaysPerSeason;

        private static readonly string[] SeasonNames = { "spring", "summer", "autumn", "winter" };

        /// <summary>The 1-based chronicle year an in-game day falls in ("the 3rd year" for day 168..251).</summary>
        public static int YearOf(int day) => day / DaysPerYear + 1;

        /// <summary>The 1-based day within its Calradian year (1..84), for a human-readable date line.</summary>
        public static int DayOfYear(int day) => day % DaysPerYear + 1;

        /// <summary>The 0..3 season index for an in-game day (0 = spring). Clamped defensively.</summary>
        public static int SeasonIndexOf(int day)
        {
            int season = day % DaysPerYear / DaysPerSeason;

            return season < 0 || season > 3 ? 0 : season;
        }

        /// <summary>The lower-case season name ("spring".."winter") for an in-game day.</summary>
        public static string SeasonNameOf(int day) => SeasonNames[SeasonIndexOf(day)];

        /// <summary>The English ordinal suffix for a small positive number ("1st", "2nd", "3rd", "4th"...).</summary>
        public static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return $"{n}th";
            switch (n % 10)
            {
                case 1:  return $"{n}st";
                case 2:  return $"{n}nd";
                case 3:  return $"{n}rd";
                default: return $"{n}th";
            }
        }
    }
}
