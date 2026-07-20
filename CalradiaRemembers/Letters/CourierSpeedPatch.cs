// Code written by Gabriel Mailhot, 05/06/2026.
// 20/07/2026: floor made model-agnostic. War Sails (NavalDLC) installs NavalDLCPartySpeedCalculationModel, which
// derives straight from PartySpeedModel (NOT from DefaultPartySpeedCalculatingModel) and has its own
// CalculateFinalSpeed, so the old static patch on the Default model never ran and couriers lost their floor. We
// now patch the CalculateFinalSpeed of whatever model the campaign is ACTUALLY running, resolved at session
// launch when every module is loaded and the model is live.

#region

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Guarantees a minimum map speed for parties this mod identifies as couriers, so they are not slowed to a
   ///   crawl (and starved) by the engine's terrain/night/size penalties on small custom parties. The floor is
   ///   attached to the ACTIVE party-speed model at session launch (see <see cref="EnsureFloorOnActiveModel" />),
   ///   which is what keeps it working under War Sails and any other mod that swaps the speed model. Courier
   ///   identity is an O(1) <see cref="CourierService.IsCourier" /> lookup, not a per-tick string comparison.
   /// </summary>
   public static class CourierSpeedPatch
   {
      /// <summary>Minimum courier speed; matches <c>CourierBaseSpeed</c> so a courier outpaces a roaming lord (~4-5).</summary>
      internal const float CourierSpeedFloor = 6.5f;

      /// <summary>
      ///   The CalculateFinalSpeed methods we have already floored this process. Prevents double-patching across
      ///   session reloads and lets the warning tell a truly unreachable model from one whose floor is live.
      /// </summary>
      private static readonly HashSet<MethodBase> PatchedMethods = new HashSet<MethodBase>();

      private static Harmony _harmony;

      /// <summary>
      ///   Attaches the courier floor to the party-speed model the campaign is actually running (vanilla's
      ///   DefaultPartySpeedCalculatingModel, War Sails' NavalDLCPartySpeedCalculationModel, or a mod's), whatever
      ///   it is. Idempotent: safe to call on every session launch. Warns ONLY when the floor genuinely could not
      ///   be attached, so a recognised DLC model no longer trips a false "another mod" alarm.
      /// </summary>
      public static void EnsureFloorOnActiveModel()
      {
         try
         {
            object model = Campaign.Current?.Models?.PartySpeedCalculatingModel;
            if (model == null) return;

            MethodInfo target = AccessTools.Method(model.GetType(), "CalculateFinalSpeed");
            if (target == null)
            {
               ModLog.Info($"[COURIER] WARNING: the active party-speed model ({model.GetType().Name}) has no " +
                           "CalculateFinalSpeed we can reach; the courier speed floor could not be attached, so " +
                           "couriers may crawl or starve. (Letters still arrive by their date-based fallback.)");
               return;
            }

            if (PatchedMethods.Contains(target)) return;

            _harmony ??= new Harmony("CalradiaRemembers.CourierSpeedFloor");
            _harmony.Patch(target, postfix: new HarmonyMethod(typeof(CourierSpeedPatch), nameof(Postfix)));
            PatchedMethods.Add(target);
            ModLog.Info($"[COURIER] speed floor attached to {model.GetType().Name}.CalculateFinalSpeed.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COURIER] failed to attach speed floor (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Forces a minimum speed for any party identified as a courier. Reads the party from the raw argument
      ///   array (<c>__args[0]</c>) rather than a named parameter, because a model override may rename it.
      /// </summary>
      public static void Postfix(object[] __args, ref ExplainedNumber __result)
      {
         MobileParty mobileParty = __args != null && __args.Length > 0 ? __args[0] as MobileParty : null;
         if (mobileParty == null) return;
         if (!CourierService.IsCourier(mobileParty.StringId)) return;

         float currentSpeed = __result.ResultNumber;
         if (currentSpeed < CourierSpeedFloor)
            __result.Add(CourierSpeedFloor - currentSpeed, new TextObject("Courier fast-track"));
      }
   }
}
