// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026, Bridges to Remember on 2.6.10): "CR records marriages as world news, but not divorces
// made by other mods (Hero.Spouse set to null)." The game raises no event when a spouse is cleared, so the marriages
// seen yesterday are compared with today's: a couple parted while both still live is a divorce. A death is not, and a
// divorce CR made itself already carries its own consequences.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>A married couple, by hero id; <see cref="Key" /> is the same whichever spouse is named first.</summary>
   public sealed class SpousePair
   {
      /// <summary>One spouse's hero id.</summary>
      public string A { get; init; } = "";

      /// <summary>The other spouse's hero id.</summary>
      public string B { get; init; } = "";

      /// <summary>The pair's order-free key.</summary>
      public string Key => ForeignDivorceDetection.PairKey(A, B);
   }

   /// <summary>What a divorce check compares.</summary>
   public sealed class DivorceDetectionFacts
   {
      /// <summary>Each married hero's spouse, as last seen. A pair appears under both ids.</summary>
      public IReadOnlyDictionary<string, string> Before { get; init; } = new Dictionary<string, string>();

      /// <summary>Each married hero's spouse, now.</summary>
      public IReadOnlyDictionary<string, string> Now { get; init; } = new Dictionary<string, string>();

      /// <summary>Heroes still in the world (alive, not disabled). A spouse missing from it was widowed, not divorced.</summary>
      public ISet<string> InTheWorld { get; init; } = new HashSet<string>();

      /// <summary>Pair keys CR dissolved itself since the last look; those consequences are already applied.</summary>
      public ISet<string> DissolvedByCr { get; init; } = new HashSet<string>();
   }

   /// <summary>Finds the marriages another mod ended. Pure.</summary>
   public static class ForeignDivorceDetection
   {
      /// <summary>What each former spouse remembers: the marriage is over, not that the other has died.</summary>
      public static string MemoryLine(string formerSpouse) => $"My marriage to {formerSpouse} has ended. We are no longer wed.";

      /// <summary>How the world tells it.</summary>
      public static string Summary(string first, string second) => $"{first} and {second} are no longer wed.";

      /// <summary>An order-free key for a couple.</summary>
      public static string PairKey(string a, string b)
         => string.CompareOrdinal(a ?? "", b ?? "") <= 0 ? $"{a}|{b}" : $"{b}|{a}";

      /// <summary>Couples wed before and no longer wed to each other now, both still in the world, not parted by CR.</summary>
      public static IReadOnlyList<SpousePair> Dissolved(DivorceDetectionFacts f)
      {
         if (f?.Before == null) return Array.Empty<SpousePair>();

         var seen = new HashSet<string>();
         var found = new List<SpousePair>();

         foreach (KeyValuePair<string, string> wed in f.Before)
         {
            string a = wed.Key, b = wed.Value;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) continue;

            string key = PairKey(a, b);
            if (!seen.Add(key)) continue;

            bool stillWed = f.Now != null && f.Now.TryGetValue(a, out string now) && now == b;
            if (stillWed) continue;
            if (f.InTheWorld == null || !f.InTheWorld.Contains(a) || !f.InTheWorld.Contains(b)) continue;
            if (f.DissolvedByCr != null && f.DissolvedByCr.Contains(key)) continue;

            found.Add(string.CompareOrdinal(a, b) <= 0 ? new SpousePair {A = a, B = b} : new SpousePair {A = b, B = a});
         }

         return found;
      }
   }
}
