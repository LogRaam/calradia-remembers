// Code written by Gabriel Mailhot, 22/10/2026.
// What an edict does, applied through the game's own settlement models (Gabriel, 21/10/2026: edicts on towns and realms,
// decreed by the player and by the world's lords). Each model's result gains one described line ("Tax relief of Derthert:
// +1"), so the town's own tooltip shows the edict and the player can check every number: never a hidden write. Attached
// to the model the campaign is ACTUALLY running, resolved at session launch (the War Sails lesson in CourierSpeedPatch:
// a DLC or a mod may replace the default model). A postfix never throws.

#region

using System;
using System.Collections.Generic;
using System.Reflection;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Edicts;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Edicts
{
   /// <summary>Adds the edict holding on a town to the game's own loyalty, security, prosperity, militia, food and tax.</summary>
   public static class EdictModelPatches
   {
      private static readonly HashSet<MethodBase> Patched = new();

      private static Harmony _harmony;

      /// <summary>The methods attached this process, by model and method (read by cr.testall).</summary>
      internal static readonly List<string> Attached = new();

      /// <summary>Attaches every channel to the live models; idempotent, safe on every session launch.</summary>
      public static void EnsureOnActiveModels()
      {
         var models = Campaign.Current?.Models;
         if (models == null) return;

         Attach(models.SettlementLoyaltyModel, "CalculateLoyaltyChange", nameof(Loyalty));
         Attach(models.SettlementSecurityModel, "CalculateSecurityChange", nameof(Security));
         Attach(models.SettlementProsperityModel, "CalculateProsperityChange", nameof(Prosperity));
         Attach(models.SettlementMilitiaModel, "CalculateMilitiaChange", nameof(Militia));
         Attach(models.SettlementFoodModel, "CalculateTownFoodStocksChange", nameof(Food));
         Attach(models.SettlementTaxModel, "CalculateTownTax", nameof(Tax));
      }

      private static void Attach(object model, string method, string postfix)
      {
         try
         {
            if (model == null) return;
            MethodInfo target = AccessTools.Method(model.GetType(), method);
            if (target == null)
            {
               ModLog.Info($"[EDICT] WARNING: {model.GetType().Name} has no {method} we can reach; edicts will not move that channel.");
               return;
            }

            if (Patched.Contains(target)) return;
            _harmony ??= new Harmony("CalradiaRemembers.Edicts");
            _harmony.Patch(target, postfix: new HarmonyMethod(typeof(EdictModelPatches), postfix));
            Patched.Add(target);
            Attached.Add($"{model.GetType().Name}.{method}");
            ModLog.Info($"[EDICT] attached to {model.GetType().Name}.{method}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[EDICT] could not attach to {method} (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>The edict holding on the town the model was asked about, from its raw first argument (an override may rename it).</summary>
      private static Edict On(object[] args)
      {
         object first = args != null && args.Length > 0 ? args[0] : null;
         Town town = first as Town ?? (first as Settlement)?.Town;

         return town == null ? null : EdictBehavior.Instance?.HoldingOn(town);
      }

      private static void AddDay(object[] args, ref ExplainedNumber result, Func<EdictEffect, float> channel)
      {
         try
         {
            Edict e = On(args);
            float v = e == null ? 0f : channel(e.Effect());
            if (v != 0f) result.Add(v, new TextObject(e.Name));
         }
         catch
         {
            // A postfix never breaks the game's own number.
         }
      }

      public static void Loyalty(object[] __args, ref ExplainedNumber __result) => AddDay(__args, ref __result, f => f.Loyalty);

      public static void Security(object[] __args, ref ExplainedNumber __result) => AddDay(__args, ref __result, f => f.Security);

      public static void Prosperity(object[] __args, ref ExplainedNumber __result) => AddDay(__args, ref __result, f => f.Prosperity);

      public static void Militia(object[] __args, ref ExplainedNumber __result) => AddDay(__args, ref __result, f => f.Militia);

      public static void Food(object[] __args, ref ExplainedNumber __result) => AddDay(__args, ref __result, f => f.Food);

      public static void Tax(object[] __args, ref ExplainedNumber __result)
      {
         try
         {
            Edict e = On(__args);
            if (e != null && e.Effect().TaxFactor != 0f) __result.AddFactor(e.Effect().TaxFactor, new TextObject(e.Name));
         }
         catch
         {
            // A postfix never breaks the game's own number.
         }
      }
   }
}
