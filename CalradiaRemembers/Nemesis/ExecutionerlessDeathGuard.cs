// Code written by Gabriel Mailhot, 15/11/2026.
// Bannerlord 1.5.7 and an execution with no executioner: see ExecutionerlessDeathPolicy. Gabriel's play test of
// 15/11/2026 crashed in ExecutionCampaignBehavior.OnHeroKilled, which reads killer.Clan whenever the detail is Executed.
// This prefix hands that one method a plain death instead when nobody did the executing, so its feud ledger is skipped
// and the rest of it (the pending-execution lists) still runs; every other listener hears the event as it was.
// Resolved by name, like LeaderlessFeudGuard: on 1.4.8, which has no such behaviour, it stays off (logged).

#region

using System;
using System.Reflection;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Nemesis
{
   /// <summary>Keeps the engine's execution bookkeeping from reading a missing executioner (a stale Executed death mark).</summary>
   public static class ExecutionerlessDeathGuard
   {
      private const string TargetType = "TaleWorlds.CampaignSystem.CampaignBehaviors.ExecutionCampaignBehavior";
      private const string TargetMethod = "OnHeroKilled";

      private static Harmony _harmony;

      /// <summary>True once attached to the live engine method (cr.test executionerless_death asserts it where the method exists).</summary>
      public static bool IsPatched { get; private set; }

      /// <summary>True when this game version has the method at all (1.5.7 and later).</summary>
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
               ModLog.Compat($"[NEMESIS] executionerless-death guard not attached: {TargetType}.{TargetMethod} not found (a game without it needs none).");

               return;
            }

            _harmony ??= new Harmony("CalradiaRemembers.ExecutionerlessDeathGuard");
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(ExecutionerlessDeathGuard), nameof(Prefix)));
            IsPatched = true;
            ModLog.Info($"[NEMESIS] executionerless-death guard attached to {TargetType}.{TargetMethod}.");
         }
         catch (Exception ex)
         {
            ModLog.Compat($"[NEMESIS] executionerless-death guard failed to attach (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>Turns an Executed detail with no killer into a plain death, for this method only. Always lets it run.</summary>
      public static bool Prefix(Hero victim, Hero killer, ref KillCharacterAction.KillCharacterActionDetail detail)
      {
         bool isExecution = detail == KillCharacterAction.KillCharacterActionDetail.Executed;
         if (!isExecution || ExecutionerlessDeathPolicy.KeptAsExecution(true, killer != null)) return true;

         ModLog.Info($"[NEMESIS] {victim?.Name} died under an execution mark with no executioner; the blood-feud ledger skips it.");
         detail = KillCharacterAction.KillCharacterActionDetail.None;

         return true;
      }
   }
}
