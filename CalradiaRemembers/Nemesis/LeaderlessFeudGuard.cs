// Code written by Gabriel Mailhot, 13/11/2026.
// Bannerlord 1.5.7 blood feuds and a leaderless clan: see LeaderlessFeudPolicy. Executing a CR nemesis (a hero of a bandit
// clan, which has no leader) made the engine's ExecutionCampaignBehavior.OnPlayerExecutedHero start a feud and set a
// relation with a null clan leader, which threw inside the kill. This guard skips the feud's start for such a clan, on
// every road an execution takes (the chat's execute_prisoner, the game's own menus). Resolved by name: on 1.4.8, where
// blood feuds do not exist, the type is missing and the guard stays off (logged), never breaking the boot.

#region

using System;
using System.Reflection;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Nemesis
{
   /// <summary>Skips the engine's blood-feud start when the executed hero's clan has no leader (a CR nemesis's bandit clan).</summary>
   public static class LeaderlessFeudGuard
   {
      private const string TargetType = "TaleWorlds.CampaignSystem.CampaignBehaviors.ExecutionCampaignBehavior";
      private const string TargetMethod = "OnPlayerExecutedHero";

      private static Harmony _harmony;

      /// <summary>True once attached to the live engine method (cr.test leaderless_feud_guard asserts it where the method exists).</summary>
      public static bool IsPatched { get; private set; }

      /// <summary>True when this game version has the blood-feud method at all (1.5.7 and later).</summary>
      public static bool TargetExists => AccessTools.TypeByName(TargetType) is Type t && AccessTools.Method(t, TargetMethod) != null;

      /// <summary>Attaches the guard. Idempotent; logs and stays off when the game has no such method.</summary>
      public static void EnsurePatched()
      {
         if (IsPatched) return;

         try
         {
            Type type = AccessTools.TypeByName(TargetType);
            MethodInfo target = type == null ? null : AccessTools.Method(type, TargetMethod);
            if (target == null)
            {
               ModLog.Compat($"[NEMESIS] leaderless-feud guard not attached: {TargetType}.{TargetMethod} not found (a game without blood feuds needs none).");

               return;
            }

            _harmony ??= new Harmony("CalradiaRemembers.LeaderlessFeudGuard");
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(LeaderlessFeudGuard), nameof(Prefix)));
            IsPatched = true;
            ModLog.Info($"[NEMESIS] leaderless-feud guard attached to {TargetType}.{TargetMethod}.");
         }
         catch (Exception ex)
         {
            ModLog.Compat($"[NEMESIS] leaderless-feud guard failed to attach (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>False (skip the feud) when the executed hero's clan has no leader; the engine runs as written otherwise.</summary>
      public static bool Prefix(Hero victim)
      {
         if (LeaderlessFeudPolicy.MayStartFeud(victim?.Clan?.Leader != null)) return true;

         ModLog.Info($"[NEMESIS] {victim?.Name} of {victim?.Clan?.Name} was executed; their clan has no leader, so no blood feud is started.");

         return false;
      }
   }
}
