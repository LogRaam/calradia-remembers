// Code written by Gabriel Mailhot, 17/11/2026.
// bdionysis80 (Nexus, 17/11/2026): his wife, pregnant a second time, "kept going back to the first pregnancy, which had
// already been completed". Nothing told her the first one had ended: a birth wrote no memory, so an old "I am with child"
// from an earlier talk could stand with nothing after it. The mother now remembers each birth, in her own words. The
// father is never named here: a child of a lover stays the bastard pillar's secret, and the birth itself is no secret.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>One child born alive.</summary>
   public sealed class BornChild
   {
      /// <summary>The name the child was given.</summary>
      public string Name { get; init; }

      /// <summary>A daughter rather than a son.</summary>
      public bool IsFemale { get; init; }
   }

   /// <summary>What is known of one birth.</summary>
   public sealed class BirthFacts
   {
      /// <summary>The children born alive, in the order the game gave them.</summary>
      public IReadOnlyList<BornChild> Alive { get; init; }

      /// <summary>How many were born dead.</summary>
      public int Stillborn { get; init; }
   }

   /// <summary>The mother's memory of a birth. Pure.</summary>
   public static class BirthMemoryText
   {
      /// <summary>"I gave birth to my son Aldric." Null when nothing was born (nothing to remember).</summary>
      public static string Compose(BirthFacts f)
      {
         List<BornChild> alive = f?.Alive?.Where(c => c != null).ToList() ?? new List<BornChild>();
         int dead = f?.Stillborn > 0 ? f.Stillborn : 0;
         if (alive.Count == 0 && dead == 0) return null;

         // The pregnancy is over: said outright, because the stale memory this replaces is "I am with child".
         string ended = " That pregnancy is over.";
         if (alive.Count == 0)
            return (dead == 1 ? "My child was born dead." : $"I gave birth to {dead} children, all born dead.") + ended;

         string born = alive.Count == 1
            ? $"I gave birth to my {Kind(alive[0])} {alive[0].Name}."
            : $"I gave birth to {Count(alive.Count)}: {Names(alive)}.";
         if (dead > 0) born += dead == 1 ? " Another was born dead." : $" {dead} more were born dead.";

         return born + ended;
      }

      #region private

      private static string Kind(BornChild c) => c.IsFemale ? "daughter" : "son";

      private static string Count(int n) => n == 2 ? "twins" : n == 3 ? "triplets" : $"{n} children";

      private static string Names(List<BornChild> alive)
      {
         List<string> parts = alive.Select(c => $"my {Kind(c)} {c.Name}").ToList();

         return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts.Last();
      }

      #endregion
   }
}
