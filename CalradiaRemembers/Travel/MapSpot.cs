// Code written by Gabriel Mailhot, 19/10/2026.
// Where a party can be put on the campaign map. cr.test authored_quest_opening (Gabriel, 19/10/2026) failed with
// "[TALE] band spawn failed: AccessViolationException": the tale's band was placed at the village plus a random
// offset. Creating a party with a spawn radius runs NavigationHelper.FindReachablePointAroundPosition, whose native
// path query starts from the given point's navigation face; a point off the walkable map has no valid face, and the
// native call reads protected memory. Every CR spawn (tale bands, couriers, bounty hunters, sergeants, the ambush
// convoy) goes through here, so none hands the engine a point it cannot stand on.

#region

using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.Travel
{
   /// <summary>Walkable ground for a new party.</summary>
   public static class MapSpot
   {
      /// <summary>
      ///   <paramref name="at" /> when a land party can stand there; else a point the game picks around
      ///   <paramref name="fallback" />'s gate (BanditSpawnCampaignBehavior's own way); else null, and the caller spawns
      ///   nothing. <paramref name="who" /> names the party in the log when it is moved or refused.
      /// </summary>
      public static CampaignVec2? Walkable(Vec2 at, Settlement fallback, string who)
      {
         var spot = new CampaignVec2(at, true);
         if (NavigationHelper.IsPositionValidForNavigationType(spot, MobileParty.NavigationType.Default)) return spot;

         if (fallback != null && NavigationHelper.IsPositionValidForNavigationType(fallback.GatePosition, MobileParty.NavigationType.Default))
         {
            ModLog.Info($"[SPAWN] {who}: ({at.x:0.0}, {at.y:0.0}) is not walkable ground; placed by the game around {fallback.Name}.");

            return NavigationHelper.FindPointAroundPosition(fallback.GatePosition, MobileParty.NavigationType.Default, 3f);
         }

         ModLog.Info($"[SPAWN] {who}: ({at.x:0.0}, {at.y:0.0}) is not walkable ground and there is no place to fall back on; not spawned.");

         return null;
      }
   }
}
