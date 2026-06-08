// Code written by Gabriel Mailhot, 05/06/2026.

#region

using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Patches the game's speed model to ensure that couriers created by this mod
   ///   actually move at their intended speed. Bannerlord's complex speed
   ///   calculations (terrain, night, size penalties) often slow down small
   ///   custom parties significantly; this patch guarantees a high minimum.
   ///   Courier identity is resolved via <see cref="CourierService.IsCourier" /> —
   ///   an O(1) HashSet lookup — rather than a display-string comparison on every
   ///   party every tick.
   /// </summary>
   [HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel), "CalculateFinalSpeed")]
   public static class CourierSpeedPatch
   {
      /// <summary>
      ///   Forces a minimum speed for any party identified as a courier.
      ///   The floor matches <c>CourierBaseSpeed</c> (6.5) so the courier is
      ///   always faster than a typical roaming lord (≈ 4–5).
      /// </summary>
      public static void Postfix(MobileParty mobileParty, ref ExplainedNumber __result)
      {
         if (mobileParty == null) return;
         if (!CourierService.IsCourier(mobileParty.StringId)) return;

         float currentSpeed = __result.ResultNumber;
         if (currentSpeed < 6.5f) __result.Add(6.5f - currentSpeed, new TextObject("Courier fast-track"));
      }
   }
}