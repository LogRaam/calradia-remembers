// Code written by Gabriel Mailhot, 23/06/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>One candidate town the companion could gather news at, named for matching the request.</summary>
   public sealed class NewsDestinationCandidate
   {
      public NewsDestinationCandidate(string settlementName, string factionName, string cultureName)
      {
         SettlementName = settlementName;
         FactionName = factionName;
         CultureName = cultureName;
      }

      public string CultureName { get; }
      public string FactionName { get; }
      public string SettlementName { get; }
   }

   /// <summary>
   ///   Chooses which town a "bring me news about X" errand rides to. Precedence: a named settlement,
   ///   then the faction (kingdom), then the culture. Candidates are host-ordered by preference, so a
   ///   tie resolves to the first (e.g. nearest). Returns -1 when the request is empty or nothing matches
   ///   — the host then falls back to the nearest town.
   /// </summary>
   public static class NewsDestinationSelector
   {
      private const int CultureMatch = 1;
      private const int FactionMatch = 2;
      private const int SettlementMatch = 3;

      public static int Choose(string about, IReadOnlyList<NewsDestinationCandidate> candidates)
      {
         if (string.IsNullOrWhiteSpace(about) || candidates == null || candidates.Count == 0) return -1;

         string q = about.Trim();
         int bestIndex = -1;
         var bestScore = 0;

         for (var i = 0; i < candidates.Count; i++)
         {
            int score = Score(q, candidates[i]);

            if (score <= bestScore) continue; // strict: the first (host-ordered) candidate keeps a tie
            bestScore = score;
            bestIndex = i;
         }

         return bestIndex;
      }

      #region private

      private static bool Contains(string field, string q)
         => !string.IsNullOrEmpty(field) && field.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

      private static int Score(string q, NewsDestinationCandidate c)
      {
         if (c == null) return 0;
         if (Contains(c.SettlementName, q)) return SettlementMatch;
         if (Contains(c.FactionName, q)) return FactionMatch;
         if (Contains(c.CultureName, q)) return CultureMatch;

         return 0;
      }

      #endregion
   }
}