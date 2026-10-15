// Code written by Gabriel Mailhot, 15/10/2026.
// How a dead relative is worded in an NPC's RELATIONSHIPS block. Maifailun (reported by tashmetu, 15/10/2026): the
// player's wife Debana died in childbirth; her father Doux Oros asked "Which Debana?" about his own daughter, because the
// block listed only living children, so his dead daughter was simply absent. A parent, child, sibling or spouse who
// died is named as dead, with how and how long ago when the game recorded it, and a kin once wed to the player is
// said to be so, since that is often why the player speaks of them. Pure so the wording is pinned by tests; the host
// reads the engine's death mark and day.

#region

using NpcMemoryService.Core.Calendar;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What is known of a relative's death.</summary>
    public sealed class KinDeathFacts
    {
        /// <summary>The engine's death mark, by name ("DiedInLabor", "DiedInBattle"...); null or unknown reads "died".</summary>
        public string Cause { get; init; }

        /// <summary>Days since the death; null when the game kept no usable record (deaths before the campaign).</summary>
        public double? DaysAgo { get; init; }

        /// <summary>The dead relative was once wed to the player (widowed or divorced: the game keeps both as former spouses).</summary>
        public bool WasPlayersSpouse { get; init; }

        /// <summary>The dead relative is a woman (the player's wife rather than husband).</summary>
        public bool IsFemale { get; init; }
    }

    /// <summary>The clause that follows a dead relative's name: " (who died in childbirth 12 days ago)".</summary>
    public static class KinDeathDescriber
    {
        /// <summary>Beyond this many days (about a hundred Calradian years) a death day is not real campaign history.</summary>
        public const double OldestRealDeathDays = 8400;

        /// <summary>
        ///   " (who fell in battle 6 days ago)", " (once the player's wife, who died in childbirth this very day)", or ""
        ///   when nothing true can be said beyond "the late". A missing, negative or absurd day says no time rather than a
        ///   false one (a tester's father dead last week in a siege was "three winters ago" without it).
        /// </summary>
        public static string Clause(KinDeathFacts f)
        {
            if (f == null) return "";

            string spouse = f.WasPlayersSpouse ? (f.IsFemale ? "once the player's wife" : "once the player's husband") : null;
            string when = When(f.DaysAgo);
            if (when == null) return spouse == null ? "" : $" ({spouse})";

            string death = $"who {CauseWords(f.Cause)} {when}";

            return spouse == null ? $" ({death})" : $" ({spouse}, {death})";
        }

        private static string CauseWords(string cause)
        {
            switch (cause)
            {
                case "DiedInBattle": return "fell in battle";
                case "DiedOfOldAge": return "died of old age";
                case "Executed":
                case "ExecutionAfterMapEvent": return "was executed";
                case "Murdered": return "was murdered";
                case "DiedInLabor": return "died in childbirth";
                default: return "died";
            }
        }

        private static string When(double? daysAgo)
        {
            if (daysAgo == null || daysAgo < 0 || daysAgo > OldestRealDeathDays) return null;
            if (daysAgo < 1) return "this very day";
            if (daysAgo < WorldCalendar.DaysPerYear) return $"{(int) daysAgo} days ago";

            return $"about {(int) (daysAgo / WorldCalendar.DaysPerYear)} year(s) ago";
        }
    }
}
