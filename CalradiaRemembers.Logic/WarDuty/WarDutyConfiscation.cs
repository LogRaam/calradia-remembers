// Code written by Gabriel Mailhot, 15/10/2026.
// Which fief a liege takes from a vassal who neither fought nor gave anything (Gabriel, 15/10/2026: "il pourrait etre
// confisque d'un fief"). ONE fief, never one under siege (the engine refuses to hand it over mid-siege, and taking it
// from the vassal defending it would be absurd), and the least of them: a castle before a town, the poorer first. The
// punishment is meant to sting, not to strip the house of its seat.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.WarDuty
{
   /// <summary>A fief of the player's clan, as the confiscation reads it.</summary>
   public sealed class FiefCandidate
   {
      /// <summary>The settlement's id.</summary>
      public string Id { get; init; }

      /// <summary>A town (false: a castle).</summary>
      public bool IsTown { get; init; }

      /// <summary>Its prosperity.</summary>
      public float Prosperity { get; init; }

      /// <summary>It is under siege.</summary>
      public bool UnderSiege { get; init; }
   }

   /// <summary>Pure: picks the fief a liege confiscates.</summary>
   public static class WarDutyConfiscation
   {
      /// <summary>True when the clan holds a fief that could be taken.</summary>
      public static bool HasTakeable(IEnumerable<FiefCandidate> fiefs) => Choose(fiefs) != null;

      /// <summary>The fief taken, or null when none can be.</summary>
      public static FiefCandidate Choose(IEnumerable<FiefCandidate> fiefs)
         => (fiefs ?? Enumerable.Empty<FiefCandidate>())
            .Where(f => f != null && !string.IsNullOrWhiteSpace(f.Id) && !f.UnderSiege)
            .OrderBy(f => f.IsTown)
            .ThenBy(f => f.Prosperity)
            .ThenBy(f => f.Id, System.StringComparer.Ordinal)
            .FirstOrDefault();
   }
}
