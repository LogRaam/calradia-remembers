// Code written by Gabriel Mailhot, 17/10/2026.
// The patrolling companion's letters. Gabriel (16/10/2026): "A chaque mois, le compagnon peut ecrire pour donner des
// nouvelles au joueur et lui demander s'il doit continuer la patrouille", and the patrol goes on until the player says
// stop, in chat or by letter. The monthly letter's Context is the patrol's real record (the model is told to report
// nothing else); the player's letter carries a stop as a control line, "[PATROL] stop", since a letter may emit no
// [ACTION]. No line means the patrol goes on.

#region

using System;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The record a monthly patrol letter reports.</summary>
    public sealed class PatrolReportFacts
    {
        /// <summary>The settlement patrolled.</summary>
        public string SettlementName { get; init; }

        /// <summary>Days since the patrol rode out.</summary>
        public int DaysOut { get; init; }

        /// <summary>Healthy men who rode out.</summary>
        public int MenAtDeparture { get; init; }

        /// <summary>Healthy men in the band now.</summary>
        public int MenFitNow { get; init; }

        /// <summary>Battles won.</summary>
        public int BattlesWon { get; init; }

        /// <summary>Battles lost.</summary>
        public int BattlesLost { get; init; }

        /// <summary>Men the beaten foes brought to the field, summed.</summary>
        public int FoesBeaten { get; init; }
    }

    /// <summary>Pure: the monthly patrol letter's Context, and reading a stop order out of a letter's answer.</summary>
    public static class PatrolLetterText
    {
        private const string StopMarker = "[PATROL]";

        /// <summary>The Context of the monthly letter: the place, the days, the fights, the men; nothing else.</summary>
        public static string Context(PatrolReportFacts f)
        {
            if (f == null) return string.Empty;
            string place = string.IsNullOrWhiteSpace(f.SettlementName) ? "the place you were sent to" : f.SettlementName.Trim();
            string fights = f.BattlesWon + f.BattlesLost == 0
                ? "No fight has come your way."
                : $"You have won {f.BattlesWon} fight{(f.BattlesWon == 1 ? "" : "s")}{(f.BattlesWon > 0 && f.FoesBeaten > 0 ? $", against {f.FoesBeaten} men in all" : "")}, and lost {f.BattlesLost}.";

            return $"You have patrolled around {place} for {f.DaysOut} days, with the {f.MenAtDeparture} men the player gave you. {fights} {f.MenFitNow} of your men are fit now.";
        }

        /// <summary>True when the answer carries the line ordering the patrol home ("[PATROL] stop").</summary>
        public static bool OrdersStop(string raw)
            => !string.IsNullOrEmpty(raw)
               && raw.Split('\n').Any(l => l.Trim().StartsWith(StopMarker, StringComparison.OrdinalIgnoreCase)
                                           && l.IndexOf("stop", StringComparison.OrdinalIgnoreCase) > 0);

        /// <summary>The answer without its "[PATROL]" control line (metadata, never part of the letter).</summary>
        public static string StripOrderLine(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.IndexOf(StopMarker, StringComparison.OrdinalIgnoreCase) < 0) return raw;

            return string.Join("\n", raw.Split('\n').Where(l => !l.Trim().StartsWith(StopMarker, StringComparison.OrdinalIgnoreCase))).TrimEnd();
        }
    }
}
