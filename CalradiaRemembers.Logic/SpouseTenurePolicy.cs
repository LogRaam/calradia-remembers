// Code written by Gabriel Mailhot, 01/10/2026.
// raphareish (Nexus, 01/10/2026): "An NPC had just gotten married (I had recently seen the notification in the chat log)
// and spoke of their spouse as if they were married for years." The prompt named the spouse and nothing else, and a
// model told "Spouse: X" assumes a marriage of long standing. The game keeps no wedding date, so CR records the day of
// every wedding it sees (WeddingDayBehavior) and the spouse line says how recent it is. A marriage CR never saw (an
// old save, a campaign that starts married) has no date; since fkasad's note (10/10/2026) the couple's eldest child, when
// old enough, says it is not new.

#region

using NpcMemoryService.Core.Calendar;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How long a character has been married, as their family line says it. Pure.</summary>
    public static class SpouseTenurePolicy
    {
        /// <summary>A wedding within this many days is a new one: a season of the world's own calendar.</summary>
        public static int NewlywedDays => WorldCalendar.DaysPerSeason;

        /// <summary>A wedding within this many days is still this year's: a year of the world's own calendar.</summary>
        public static int ThisYearDays => WorldCalendar.DaysPerYear;

        /// <summary>
        ///   The clause after the spouse's name: ", wed today: you are newlyweds", ", wed 3 days ago: you are newlyweds",
        ///   ", wed less than a year ago"; empty for a marriage of long standing or one whose day is unknown.
        /// </summary>
        public static string Clause(int? weddedOnDay, int today)
        {
            if (weddedOnDay is not int wed) return "";

            int days = today - wed;
            if (days < 0) return "";
            if (days == 0) return ", wed today: you are newlyweds";
            if (days <= NewlywedDays) return $", wed {days} day{(days == 1 ? "" : "s")} ago: you are newlyweds";

            return days <= ThisYearDays ? ", wed less than a year ago" : "";
        }

        /// <summary>A child this old proves the marriage is not new.</summary>
        public const int YearsThatProveAMarriage = 2;

        /// <summary>
        ///   fkasad (10/10/2026): for a marriage whose day CR never saw, the couple's eldest living child says it is not new:
        ///   ", wed for years: Elys, your eldest together, is 12". Empty without such a child, or with one too young to prove it.
        /// </summary>
        public static string LongStanding(string eldestTogether, int eldestTogetherYears)
            => string.IsNullOrWhiteSpace(eldestTogether) || eldestTogetherYears < YearsThatProveAMarriage
               ? ""
               : $", wed for years: {eldestTogether}, your eldest together, is {eldestTogetherYears}";
    }
}
