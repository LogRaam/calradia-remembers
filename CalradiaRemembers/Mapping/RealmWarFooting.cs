// Code written by Gabriel Mailhot, 30/09/2026.
// A realm's war footing, read in one place: whether it is at war, its own strength, and its enemies' combined
// strength. Moved out of MercenaryReputationBehavior (its only reader until then) when the mood of the day needed the
// same reading, so "losing a war" is measured one way in the mod (MercenaryDeparturePolicy.IsLosing judges it).

#region

using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads a realm's war footing from the live game.</summary>
   public static class RealmWarFooting
   {
      /// <summary>Whether the realm is at war, its own strength, and its enemies' combined strength.</summary>
      public static (bool AtWar, float Own, float Enemies) Of(Kingdom kingdom)
      {
         if (kingdom == null) return (false, 0f, 0f);

         List<Kingdom> enemies = (Kingdom.All ?? new List<Kingdom>())
                                 .Where(k => k != null && k != kingdom && !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(k, kingdom))
                                 .ToList();

         return (enemies.Count > 0, kingdom.CurrentTotalStrength, enemies.Sum(k => k.CurrentTotalStrength));
      }
   }
}
