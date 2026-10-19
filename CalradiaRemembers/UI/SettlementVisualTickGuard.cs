// Code written by Gabriel Mailhot, 03/09/2026.
// The third crash-safe condition for a CR panel (see CrPanelHost's header): while a panel owns the screen, skip the
// vanilla settlement-visual tick. That tick, SandBox.View.Map.Managers.SettlementVisualManager.OnTick, runs
// SettlementVisual.Tick for every settlement, whose siege-bombardment branch calls GameEntity.SetGlobalFrame on
// projectiles in flight (offset IL_0aa7, four confirmed native crashes). It runs on the FRAME loop, not the campaign
// clock, so freezing time cannot stop it. Pushing our screen suspends the map's RENDER but not this tick, so a frozen
// projectile keeps calling SetGlobalFrame under the panel and eventually access-violates. We patch the ONE method
// that drives it. Defensive by design: resolved by name so a game update that renames it degrades to "guard not
// attached" (logged) rather than breaking the build, and its own try/catch never lets a patch failure crash the boot
// or disturb the mod's other Harmony patches.

// The NOT-attached notices go through ModLog.Compat, never Info: logging is OFF by default on Nexus, so a
// player whose game build silently defeats this guard would leave no trace of it anywhere, which is exactly
// the case where the fact matters most (two crash reports, 2026-09-07, that hinge on whether it attached).

#region

using System;
using System.Reflection;
using HarmonyLib;
using CalradiaRemembers.Foundation;

#endregion

namespace CalradiaRemembers.UI
{
   /// <summary>
   ///   Harmony guard that skips <c>SettlementVisualManager.OnTick</c> while any CR panel owns the screen, so the
   ///   vanilla siege-visual tick cannot crash natively under a pushed panel. Attach once at startup via
   ///   <see cref="EnsurePatched" />; the decision to skip is <see cref="CrPanelHost.SuppressSettlementVisualTick" />.
   /// </summary>
   public static class SettlementVisualTickGuard
   {
      private const string TargetType = "SandBox.View.Map.Managers.SettlementVisualManager";
      private const string TargetMethod = "OnTick";

      /// <summary>
      ///   How many times the engine has called the guarded tick this session, counted BEFORE the skip decision.
      ///   Read by the map-tick probe (cr.tick_probe) to measure whether a given way of hosting a panel actually
      ///   silences the map underneath it. Counting is a single increment on a method the engine already calls.
      /// </summary>
      public static long CallCount;

      private static Harmony _harmony;
      private static bool _patched;

      /// <summary>True once the guard has been attached to the live engine method (a testall self-test asserts this).</summary>
      public static bool IsPatched => _patched;

      /// <summary>
      ///   Attaches the guard to the active engine's <c>SettlementVisualManager.OnTick</c>. Idempotent, and safe to
      ///   call before a campaign exists (the type lives in SandBox.View, loaded by module init). If the type or
      ///   method cannot be resolved (a future game version), it logs and leaves the guard off rather than throwing:
      ///   panels then fall back to pushed-screen + clock-freeze, the same protection as before this guard.
      /// </summary>
      public static void EnsurePatched()
      {
         if (_patched) return;

         try
         {
            Type managerType = AccessTools.TypeByName(TargetType);
            if (managerType == null)
            {
               ModLog.Compat($"[PANEL] siege-visual tick guard NOT attached: type {TargetType} not found (game version change?). Panels stay safe via pushed screen + clock freeze.");

               return;
            }

            MethodInfo target = AccessTools.Method(managerType, TargetMethod);
            if (target == null)
            {
               ModLog.Compat($"[PANEL] siege-visual tick guard NOT attached: {TargetType}.{TargetMethod} not found (game version change?). Panels stay safe via pushed screen + clock freeze.");

               return;
            }

            _harmony ??= new Harmony("CalradiaRemembers.SettlementVisualTickGuard");
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(SettlementVisualTickGuard), nameof(Prefix)));
            _patched = true;
            ModLog.Info($"[PANEL] siege-visual tick guard attached to {TargetType}.{TargetMethod}.");
         }
         catch (Exception ex)
         {
            ModLog.Compat($"[PANEL] siege-visual tick guard failed to attach (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Returns false (skip the original) while a CR panel owns the screen, so the settlement-visual tick and its
      ///   siege-bombardment SetGlobalFrame never run under the panel. The map is invisible below the panel anyway;
      ///   the next tick after the panel closes catches the visuals up in one frame. Parameterless bool prefix, so it
      ///   matches the target whatever its signature.
      /// </summary>
      public static bool Prefix()
      {
         CallCount++;

         return !CrPanelHost.SuppressSettlementVisualTick;
      }
   }
}
