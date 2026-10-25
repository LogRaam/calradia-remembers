// Code written by Gabriel Mailhot, 24/10/2026.
// Gabriel (24/10/2026): "ce ne sera pas un conte unique. Il aura des chances de se reproduire. J'aurai plusieurs videos et
// il faudrait qu'a chaque fois que l'evenement arrive, qu'on choisisse un nouveau video parmi les choix que je donnerai."
// A stage's image_pool: each playing of the tale shows one the player has not seen yet; once all are seen, they start over.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Which picture of a stage's pool this playing of the tale shows. Pure.</summary>
   public static class TaleImagePool
   {
      /// <summary>
      ///   One of <paramref name="pool" /> not in <paramref name="shownBefore" /> (all of them once every one was shown), at
      ///   <paramref name="roll" /> (0 to 1) among those; null for an empty pool.
      /// </summary>
      public static string Pick(IReadOnlyList<string> pool, IEnumerable<string> shownBefore, double roll)
      {
         List<string> all = (pool ?? Array.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
         if (all.Count == 0) return null;

         var seen = new HashSet<string>(shownBefore ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
         List<string> fresh = all.Where(p => !seen.Contains(p)).ToList();
         if (fresh.Count == 0) fresh = all;

         int i = (int) Math.Floor(Math.Max(0, Math.Min(0.999999, roll)) * fresh.Count);

         return fresh[i];
      }

      /// <summary>
      ///   What is remembered as shown once <paramref name="picked" /> is: added to <paramref name="shownBefore" />, or alone
      ///   when it starts a new round (every one of the pool was already shown).
      /// </summary>
      public static List<string> AfterShowing(IReadOnlyList<string> pool, IEnumerable<string> shownBefore, string picked)
      {
         List<string> seen = (shownBefore ?? Enumerable.Empty<string>()).ToList();
         if (string.IsNullOrWhiteSpace(picked)) return seen;

         var all = new HashSet<string>((pool ?? Array.Empty<string>()).Select(p => p?.Trim()), StringComparer.OrdinalIgnoreCase);
         bool newRound = all.Count > 0 && all.All(p => seen.Contains(p, StringComparer.OrdinalIgnoreCase));
         if (newRound) seen.Clear();
         if (!seen.Contains(picked, StringComparer.OrdinalIgnoreCase)) seen.Add(picked);

         return seen;
      }
   }
}
