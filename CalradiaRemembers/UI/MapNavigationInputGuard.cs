// Code written by Gabriel Mailhot, 06/09/2026.
// Player report (Gabriel, testing 2026-09-06): while typing a REPLY in the letter book, pressing the face-generator
// key opened the character editor underneath. The chat never had this, and the difference is WHERE each window
// opens: the chat runs during a conversation (a Mission owns the screen, so the map is not ticking), while the
// letter book and the Balance of Power panel sit over the LIVE campaign map, whose MapScreen.TickNavigationInput
// keeps reading its own hotkeys beneath our pushed screen.
//
// Why only that one key leaked: in TickNavigationInput every other destination is gated by the vanilla navigation
// permission system (inventory, party, clan, kingdom all ask GetPermission(...).IsAuthorized first), but the face
// generator branch calls OpenFaceGeneratorScreen() with NO permission check. There is therefore no sanctioned flag
// a mod can set to stop it, which is why this is a guard rather than a setting.
//
// Same shape as SettlementVisualTickGuard, and for the same reason (a frame-driven vanilla tick running under a
// pushed panel): resolved BY NAME so a game update that renames it degrades to "guard not attached" (logged) rather
// than breaking the build, with its own try/catch so a patch failure never disturbs boot or the mod's other patches.

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
   ///   Harmony guard that skips <c>MapScreen.TickNavigationInput</c> while a CR panel owns the screen, so map
   ///   hotkeys cannot fire under the letter book or the Balance of Power panel. Attach once at startup via
   ///   <see cref="EnsurePatched" />; the decision to skip is <see cref="CrPanelHost.AnyPanelOwnsScreen" />.
   /// </summary>
   public static class MapNavigationInputGuard
   {
      private const string TargetType = "SandBox.View.Map.MapScreen";
      private const string TargetMethod = "TickNavigationInput";

      /// <summary>
      ///   How many times the engine has called the guarded map input tick this session, counted BEFORE the skip
      ///   decision. The map-tick probe's second, independent signal: navigation input and settlement visuals are
      ///   driven from the same MapScreen frame tick, so they should fall silent together or not at all.
      /// </summary>
      public static long CallCount;

      private static Harmony _harmony;
      private static bool _patched;

      /// <summary>True once the guard has been attached to the live engine method (a cr.testall self-test asserts this).</summary>
      public static bool IsPatched => _patched;

      /// <summary>
      ///   Attaches the guard to the active engine's <c>MapScreen.TickNavigationInput</c>. Idempotent, and safe to
      ///   call before a campaign exists. If the type or method cannot be resolved (a future game version), it logs
      ///   and leaves the guard off rather than throwing: panels then behave exactly as they did before it, which is
      ///   a nuisance (a stray hotkey) and never a crash.
      /// </summary>
      public static void EnsurePatched()
      {
         if (_patched) return;

         try
         {
            Type mapScreenType = AccessTools.TypeByName(TargetType);
            if (mapScreenType == null)
            {
               ModLog.Compat($"[PANEL] map-navigation input guard NOT attached: type {TargetType} not found (game version change?). A map hotkey may still fire under a panel.");

               return;
            }

            MethodInfo target = AccessTools.Method(mapScreenType, TargetMethod);
            if (target == null)
            {
               ModLog.Compat($"[PANEL] map-navigation input guard NOT attached: {TargetType}.{TargetMethod} not found (game version change?). A map hotkey may still fire under a panel.");

               return;
            }

            _harmony ??= new Harmony("CalradiaRemembers.MapNavigationInputGuard");
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(MapNavigationInputGuard), nameof(Prefix)));
            _patched = true;
            ModLog.Info($"[PANEL] map-navigation input guard attached to {TargetType}.{TargetMethod}.");
         }
         catch (Exception ex)
         {
            ModLog.Compat($"[PANEL] map-navigation input guard failed to attach (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Skips the original while a CR panel owns the screen, reporting that no navigation input was handled this
      ///   frame (which is the truth: the player was typing into the panel, not steering the map). Leaves every
      ///   other frame untouched, so map hotkeys work exactly as before whenever no panel is open.
      /// </summary>
      public static bool Prefix(ref bool __result)
      {
         CallCount++;

         if (!CrPanelHost.AnyPanelOwnsScreen) return true;

         __result = false;

         return false;
      }
   }
}
