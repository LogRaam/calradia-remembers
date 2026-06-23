// Code written by Gabriel Mailhot, 23/06/2026.
// Companion news-errand (ROADMAP) — pure destination scoping. When the player asks for news "about X",
// this picks which candidate town the companion should ride to: a named settlement wins over a faction
// match, which wins over a culture match. The host supplies the candidates already ordered by preference
// (e.g. nearest first) so ties resolve to the closest, and resolves an empty/no-match request to the
// nearest town itself. Kept pure and unit-tested; the engine gathers the towns and does the travelling.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One candidate town the companion could gather news at, named for matching the request.</summary>
    public sealed class NewsDestinationCandidate
    {
        public string SettlementName { get; }
        public string FactionName { get; }
        public string CultureName { get; }

        public NewsDestinationCandidate(string settlementName, string factionName, string cultureName)
        {
            SettlementName = settlementName;
            FactionName = factionName;
            CultureName = cultureName;
        }
    }

    /// <summary>
    ///   Chooses which town a "bring me news about X" errand rides to. Precedence: a named settlement,
    ///   then the faction (kingdom), then the culture. Candidates are host-ordered by preference, so a
    ///   tie resolves to the first (e.g. nearest). Returns -1 when the request is empty or nothing matches
    ///   — the host then falls back to the nearest town.
    /// </summary>
    public static class NewsDestinationSelector
    {
        private const int SettlementMatch = 3;
        private const int FactionMatch    = 2;
        private const int CultureMatch    = 1;

        public static int Choose(string about, IReadOnlyList<NewsDestinationCandidate> candidates)
        {
            if (string.IsNullOrWhiteSpace(about) || candidates == null || candidates.Count == 0) return -1;

            string q = about.Trim();
            int bestIndex = -1;
            int bestScore = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                int score = Score(q, candidates[i]);
                if (score > bestScore) // strict: the first (host-ordered) candidate keeps a tie
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static int Score(string q, NewsDestinationCandidate c)
        {
            if (c == null) return 0;
            if (Contains(c.SettlementName, q)) return SettlementMatch;
            if (Contains(c.FactionName, q)) return FactionMatch;
            if (Contains(c.CultureName, q)) return CultureMatch;
            return 0;
        }

        private static bool Contains(string field, string q)
            => !string.IsNullOrEmpty(field) && field.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
