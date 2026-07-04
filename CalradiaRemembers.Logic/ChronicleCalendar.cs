// Code written by Gabriel Mailhot, 04/07/2026.
// The player chronicle groups a long life into yearly chapters. Pure date math, shared by the text
// writer (CalradiaRemembers.WorldEvents.ChronicleWriter) and the in-game viewer
// (CalradiaRemembers.Chronicle.ChronicleViewModel) so the two rendering paths never drift on what
// "year N" means. Calradia's calendar has no leap years or irregular months in this mod's model.

namespace CalradiaRemembers.Logic
{
    /// <summary>The shared calendar constants and year math for the player chronicle.</summary>
    public static class ChronicleCalendar
    {
        /// <summary>In-game days in one Calradian year, as used throughout the chronicle.</summary>
        public const int DaysPerYear = 84;

        /// <summary>The 1-based chronicle year an in-game day falls in ("the 3rd year" for day 168..251).</summary>
        public static int YearOf(int day) => day / DaysPerYear + 1;
    }
}
