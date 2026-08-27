// Code written by Gabriel Mailhot, 29/06/2026.
// The town-centre street ambush needs to start the brawl AFTER the mission has fully started — once the player
// and the injected cutthroats are spawned. None of the campaign mission-start events fit: they fire mid-AfterStart
// (before the agents spawn, and adding a behaviour there throws "Collection was modified" because Mission.AfterStart
// is iterating its behaviour list). A postfix on Mission.AfterStart is the one reliable seam: it runs after that
// iteration completes, with every agent present, and is free to add a mission behaviour.

#region

using HarmonyLib;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>
   ///   Starts a pending town-centre ambush brawl or a pending town duel, once the mission has fully started.
   /// </summary>
   [HarmonyPatch(typeof(Mission), "AfterStart")]
   public static class AmbushMissionStartPatch
   {
      /// <summary>
      ///   Both town-scene fights hang off this one seam rather than each patching Mission.AfterStart
      ///   separately: a second Harmony patch on the same method would work but would leave their relative
      ///   order implicit, and each call is a no-op unless its own state is pending, so they cannot collide.
      /// </summary>
      public static void Postfix(Mission __instance)
      {
         TownAmbushBehavior.AttachBrawlIfPending(__instance);
         Duels.TownDuelBehavior.AttachDuelIfPending(__instance);
         Chat.CaptiveSceneStager.PlaceAudienceCompanionIfPending(__instance);
      }
   }
}
