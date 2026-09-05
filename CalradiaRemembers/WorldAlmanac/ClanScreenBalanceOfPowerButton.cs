// Code written by Gabriel Mailhot, 05/09/2026.
// A player entry point for the Balance of Power panel: a button on the Clan screen, added PROGRAMMATICALLY.
//
// Why not UIExtenderEx (as the encyclopedia buttons use): on this game build (War Sails 1.4.8) neither the
// ViewModelMixin nor the PrefabExtension applied to the ClanScreen movie, confirmed in game with on-screen logging
// (the mixin constructor never ran on clan open, and no button was injected), while the same UIExtenderEx shapes
// work on EncyclopediaHeroPage. So we mirror the mod's other non-UIExtenderEx UI injection (the council map-bar
// button, MapNavigationCouncilItemPatch): a Harmony postfix that builds the button in C# and adds it to the loaded
// movie's widget tree. No mixin, no bound prefab: the click is wired straight to BalanceOfPowerManager.Open().

#region

using System;
using HarmonyLib;
using SandBox.GauntletUI;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Adds a "Balance of Power" button to the Clan screen by hand (UIExtenderEx does not apply to this movie on
   ///   the current build). A per-frame postfix on <c>GauntletClanScreen.OnFrameTick</c> injects the button once
   ///   into the loaded movie's root, above the Finance panel, and wires its click to open the World Almanac.
   /// </summary>
   [HarmonyPatch(typeof(GauntletClanScreen), "OnFrameTick")]
   internal static class ClanScreenBalanceOfPowerButton
   {
      private const string ButtonId = "CR_BalanceOfPowerButton";
      private const string MovieName = "ClanScreen";

      private static readonly AccessTools.FieldRef<GauntletClanScreen, GauntletLayer> LayerRef =
         AccessTools.FieldRefAccess<GauntletClanScreen, GauntletLayer>("_gauntletLayer");

      private static void Postfix(GauntletClanScreen __instance)
      {
         try
         {
            GauntletLayer layer = LayerRef(__instance);
            GauntletMovieIdentifier movie = layer?.GetMovieIdentifier(MovieName);
            Widget root = movie?.Movie?.RootWidget;
            if (root == null) return;

            // Idempotent: the postfix runs every frame, so add the button only once per loaded movie.
            if (root.FindChild(ButtonId, true) != null) return;

            UIContext ctx = layer.UIContext;

            // Prefer to hang the button on the Finance panel so it tracks that column (and its slide-in); fall back
            // to the movie root if the panel is not found, so the button still appears.
            Widget host = root.FindChild("FinancePanelWidget", true) ?? root;

            var button = new ButtonWidget(ctx)
            {
               Id = ButtonId,
               WidthSizePolicy = SizePolicy.Fixed,
               HeightSizePolicy = SizePolicy.Fixed,
               SuggestedWidth = 245f,
               SuggestedHeight = 42f,
               HorizontalAlignment = HorizontalAlignment.Center,
               VerticalAlignment = VerticalAlignment.Top,
               PositionYOffset = -54f,
               Brush = ctx.BrushFactory.GetBrush("ButtonBrush1")
            };

            var label = new TextWidget(ctx)
            {
               WidthSizePolicy = SizePolicy.StretchToParent,
               HeightSizePolicy = SizePolicy.StretchToParent,
               HorizontalAlignment = HorizontalAlignment.Center,
               VerticalAlignment = VerticalAlignment.Center,
               Brush = ctx.BrushFactory.GetBrush("Popup.Button.Text"),
               Text = "Balance of Power"
            };

            button.AddChild(label);
            button.ClickEventHandlers.Add(_ => BalanceOfPowerManager.Open());
            host.AddChild(button);

            ModLog.Info("[BOP-CLAN] button added to the Clan screen.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BOP-CLAN] failed to add the Clan-screen button: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
