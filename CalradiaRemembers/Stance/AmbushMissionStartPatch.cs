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
   /// <summary>Starts a pending town-centre ambush brawl once the mission has fully started.</summary>
   [HarmonyPatch(typeof(Mission), "AfterStart")]
   public static class AmbushMissionStartPatch
   {
      public static void Postfix(Mission __instance) => TownAmbushBehavior.AttachBrawlIfPending(__instance);
   }
}
