// Code written by Gabriel Mailhot, 23/08/2026.
// Gives the council bar button its picture as a real brush-layer sprite. The bar's button
// (IconOffsetButtonWidget) draws - and sizes itself from - the MapBar.Left.Button.Backgrounds layer named
// after the item's ItemId ("cr_council"); that layer is added by the CRMapBar.xml OverrideBrush with
// Sprite="CR\council_icon", but a brush layer needs a Sprite and an image file cannot be declared as one in
// XML (SpriteData sheets resolve via Texture.GetFromResource, an engine-resource name, never a loose file).
// So the sprite is built at runtime from cr_council_button.png (the council picture pre-cropped to the
// native 76x52 tile, so the image FILLS the button) by the same file-loading machinery as the chat
// portraits (RuntimePortraitFactory), injected into SpriteData.Sprites under the name the layer references,
// and ALSO assigned straight onto the live layer: brushes may already be loaded (their layer sprites
// resolved to null) before our sprite exists, while the widget re-reads its layer every frame, so a late
// assignment is picked up immediately.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Portraits;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

#endregion

namespace CalradiaRemembers.Map
{
   /// <summary>Registers the council button picture as the "CR\council_icon" sprite the MapBar.Left.Button.Backgrounds override layer points at (idempotent).</summary>
   internal static class CouncilBarIconSprite
   {
      internal const string SpriteName = @"CR\council_icon";

      // A PNG already in the engine's channel order: correctRedBlue stays FALSE (that correction is for
      // BGR-read JPGs only - see RuntimePortraitFactory.GetOrCreateCached).
      private static readonly string IconPath = System.IO.Path.Combine(
         CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_council_button.png");

      private static bool _successLogged;

      /// <summary>
      ///   Builds the sprite (cached per path), injects it into SpriteData under <see cref="SpriteName" />, and
      ///   repairs the live brush layer. Safe to call repeatedly - every step is skipped once done, so the
      ///   map-bar patch re-runs it on each map open (which also covers a SpriteData rebuilt on a language
      ///   change, an event that would silently drop the injection).
      /// </summary>
      public static void EnsureRegistered() => EnsureLayer("cr_council", SpriteName, IconPath, ref _successLogged);

      /// <summary>
      ///   The same work for any bar button of ours (the letter box's too, 30/10/2026): the sprite built from the file and
      ///   injected under its name, and the layer named after the item's id created on the live brush when missing.
      /// </summary>
      internal static void EnsureLayer(string layerName, string spriteName, string iconPath, ref bool successLogged)
      {
         try
         {
            Sprite sprite = RuntimePortraitFactory.GetOrCreateCached(iconPath, correctRedBlue: false);

            if (sprite == null)
            {
               ModLog.Error($"[COUNCIL BAR] {layerName} button sprite could not be built from {iconPath}.");

               return;
            }

            if (UIResourceManager.SpriteData != null && !UIResourceManager.SpriteData.SpriteExists(spriteName))
               UIResourceManager.SpriteData.Sprites[spriteName] = sprite;

            Brush brush = UIResourceManager.BrushFactory?.GetBrush("MapBar.Left.Button.Backgrounds");

            if (brush == null)
            {
               ModLog.Compat("[COUNCIL BAR] the map bar brush is missing entirely; the council button cannot be drawn.");

               return;
            }

            BrushLayer layer = brush.GetLayer(layerName);

            // OUR LAYER CAN BE WIPED BY A BRUSH DECLARED THE BLUNT WAY, and the asymmetry is the point.
            //
            // CRMapBar.xml does NOT redefine this brush. It declares CR.MapBar.Left.Button.Backgrounds with
            // OverrideBrush="MapBar.Left.Button.Backgrounds", the engine's own merge: the override is filled
            // from the original and our single layer is added by name, so every native entry survives - down
            // to NavalDLC's manage_fleet, which a hand-copied brush would silently freeze at whatever the
            // game shipped the day the file was written.
            //
            // Retinues v2.0.0.12 ships GUI/Brushes/MapBar.Troops.xml, which declares a brush under the NATIVE
            // NAME and restates every vanilla layer plus its own "troops". That replaces the brush whole, and
            // a replacement carries no merged override with it. Load last and it wins; our layer is simply not
            // in what the bar reads.
            //
            // The button then sits in the bar drawing NOTHING, because IconOffsetButtonWidget takes its size
            // from the sprite of the layer named after the item id - no layer, no size, no picture, and a
            // player correctly reports that the button disappeared (Gabriel reproduced it 17/09/2026: bar
            // holds 10 items, sprite has no layer, nothing on screen).
            //
            // So the layer is CREATED here when it is absent, on whatever brush is live. It costs nothing in
            // the ordinary case, where the override did its work, and it takes nothing from the other mod.
            if (layer == null)
            {
               layer = new BrushLayer {Name = layerName};
               brush.AddLayer(layer);
               ModLog.Compat($"[COUNCIL BAR] another mod replaced the map bar brush, so the {layerName} layer was "
                           + "rebuilt at runtime. The button keeps its icon and theirs is untouched.");
            }

            if (layer.Sprite != sprite) layer.Sprite = sprite;

            if (!successLogged)
            {
               successLogged = true;
               ModLog.Info($"[COUNCIL BAR] {layerName} button sprite registered on the MapBar.Left.Button.Backgrounds layer.");
            }
         }
         catch (Exception ex) { ModLog.Error($"[COUNCIL BAR] {layerName} button sprite registration failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
