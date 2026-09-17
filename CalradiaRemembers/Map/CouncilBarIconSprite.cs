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
         BasePath.Name, "Modules", "CalradiaRemembers", "GUI", "Images", "cr_council_button.png");

      private static bool _successLogged;

      /// <summary>
      ///   Builds the sprite (cached per path), injects it into SpriteData under <see cref="SpriteName" />, and
      ///   repairs the live brush layer. Safe to call repeatedly - every step is skipped once done, so the
      ///   map-bar patch re-runs it on each map open (which also covers a SpriteData rebuilt on a language
      ///   change, an event that would silently drop the injection).
      /// </summary>
      public static void EnsureRegistered()
      {
         try
         {
            Sprite sprite = RuntimePortraitFactory.GetOrCreateCached(IconPath, correctRedBlue: false);

            if (sprite == null)
            {
               ModLog.Error($"[COUNCIL BAR] council button sprite could not be built from {IconPath}.");

               return;
            }

            if (UIResourceManager.SpriteData != null && !UIResourceManager.SpriteData.SpriteExists(SpriteName))
               UIResourceManager.SpriteData.Sprites[SpriteName] = sprite;

            Brush brush = UIResourceManager.BrushFactory?.GetBrush("MapBar.Left.Button.Backgrounds");

            if (brush == null)
            {
               ModLog.Compat("[COUNCIL BAR] the map bar brush is missing entirely; the council button cannot be drawn.");

               return;
            }

            BrushLayer layer = brush.GetLayer("cr_council");

            // THE LAYER MAY BELONG TO SOMEBODY ELSE'S FILE. A Brushes XML redefines a brush WHOLE, so two mods
            // that both extend MapBar.Left.Button.Backgrounds do not merge - the one loaded last replaces the
            // other, layers and all. Retinues v2.0.0.12 ships GUI/Brushes/MapBar.Troops.xml, which redeclares
            // every vanilla layer plus its own "troops"; ours is GUI/Brushes/CRMapBar.xml, same brush plus
            // "cr_council". Whoever loses that race loses their button's ICON while the button itself sits in
            // the bar drawing nothing - which is exactly what a player sees as "the button disappeared"
            // (Gabriel reproduced it 17/09/2026: bar holds 10 items, sprite has no layer, nothing on screen).
            //
            // So the layer is CREATED here when it is absent, on the live brush, rather than relied upon to
            // have survived. It costs nothing when our file did win, and it makes the race irrelevant.
            if (layer == null)
            {
               layer = new BrushLayer {Name = "cr_council"};
               brush.AddLayer(layer);
               ModLog.Compat("[COUNCIL BAR] another mod's brush replaced ours, so the cr_council layer was "
                           + "rebuilt at runtime. The button keeps its icon and theirs is untouched.");
            }

            if (layer.Sprite != sprite) layer.Sprite = sprite;

            if (!_successLogged)
            {
               _successLogged = true;
               ModLog.Info("[COUNCIL BAR] council button sprite registered on the MapBar.Left.Button.Backgrounds layer.");
            }
         }
         catch (Exception ex) { ModLog.Error($"[COUNCIL BAR] council button sprite registration failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
