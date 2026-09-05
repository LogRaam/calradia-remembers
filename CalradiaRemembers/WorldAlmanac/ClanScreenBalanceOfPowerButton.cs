// Code written by Gabriel Mailhot, 05/09/2026.
// A player entry point for the Balance of Power panel: a button on the Clan screen.
//
// Two earlier attempts failed on this game build (War Sails 1.4.8): UIExtenderEx (neither the ViewModelMixin nor
// the PrefabExtension applied to the ClanScreen movie, confirmed in game with on-screen logging), and a hand-built
// ButtonWidget added to the movie's root (it rendered but received NO input at all, not even hover, because the
// clan screen's custom root widget does not route input to widgets added under it that way).
//
// This works instead: a Harmony postfix loads our button as a SECOND GauntletMovie on the clan screen's own layer.
// A real movie participates in the native input pipeline, so the button hovers and clicks normally; its full-screen
// root does not accept events, so every click outside the button falls through to the clan screen below. The button
// binds to ClanBalanceOfPowerButtonVM, whose command opens the World Almanac.

#region

using System;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Engine.GauntletUI;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Loads the "Balance of Power" button as an overlay movie on the Clan screen's layer (UIExtenderEx and a
   ///   hand-built widget both failed to receive input on this build). A per-frame postfix loads it once per open.
   /// </summary>
   [HarmonyPatch(typeof(GauntletClanScreen), "OnFrameTick")]
   internal static class ClanScreenBalanceOfPowerButton
   {
      private const string OverlayMovie = "CR_ClanBalanceOfPowerButton";

      private static readonly AccessTools.FieldRef<GauntletClanScreen, GauntletLayer> LayerRef =
         AccessTools.FieldRefAccess<GauntletClanScreen, GauntletLayer>("_gauntletLayer");

      private static void Postfix(GauntletClanScreen __instance)
      {
         try
         {
            GauntletLayer layer = LayerRef(__instance);
            if (layer == null) return;

            // Idempotent: OnFrameTick runs every frame, and each clan-open builds a fresh layer, so load the overlay
            // once whenever this layer does not already carry it.
            if (layer.GetMovieIdentifier(OverlayMovie) != null) return;

            layer.LoadMovie(OverlayMovie, new ClanBalanceOfPowerButtonVM());
            ModLog.Info("[BOP-CLAN] overlay button movie loaded on the Clan screen.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BOP-CLAN] failed to load the Clan-screen button movie: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
