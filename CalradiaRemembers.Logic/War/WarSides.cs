// Code written by Gabriel Mailhot, 10/09/2026.
// Extracted during the facts-object realignment. Two policies were each asking the same question of the
// same pair of faction ids and each re-deriving the comparison by hand: KeepPeaceBreachPolicy.ShouldBreach
// ("did the promisor declare on the house he swore peace with") and WarDeedMatcher.IsSatisfied ("did the
// player declare on the house the deed demanded"). Both took four bare strings in a row, both wrote their
// own string.Equals(..., OrdinalIgnoreCase), and both fail closed on a blank id for the same reason: a
// false "oathbreaker" is worse than a missed one.
//
// So the two halves of a war get a name. The comparison lives here once, which is also the only place a
// future change of comparison (culture-invariant, trimmed, whatever) would have to be made.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.War
{
   /// <summary>
   ///   The two sides of one war declaration, as faction ids. Who DECLARED and who was declared upon are
   ///   different roles with different consequences, so they are named rather than ordered: a war the
   ///   promisor was dragged into as the defender is not an oath broken, it is an invasion suffered.
   /// </summary>
   public readonly struct WarSides
   {
      public WarSides(string aggressorFactionId, string defenderFactionId)
      {
         AggressorFactionId = aggressorFactionId;
         DefenderFactionId = defenderFactionId;
      }

      /// <summary>The faction that declared the war.</summary>
      public string AggressorFactionId { get; }

      /// <summary>The faction the war was declared upon.</summary>
      public string DefenderFactionId { get; }

      /// <summary>Both sides resolved to a real id. An unresolved side is never read as a match.</summary>
      public bool IsResolved
         => !string.IsNullOrWhiteSpace(AggressorFactionId) && !string.IsNullOrWhiteSpace(DefenderFactionId);

      /// <summary>True when this faction declared the war. A blank id never matches.</summary>
      public bool AggressorIs(string factionId)
         => Same(AggressorFactionId, factionId);

      /// <summary>True when the war was declared upon this faction. A blank id never matches.</summary>
      public bool DefenderIs(string factionId)
         => Same(DefenderFactionId, factionId);

      #region private

      private static bool Same(string a, string b)
         => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

      #endregion
   }
}
