// Code written by Gabriel Mailhot, 30/10/2026.
// Gives the letter box's bar button its picture (Gabriel, 30/10/2026: "on va creer notre propre image"), the same way as
// the council's: cr_letterbox_button.png, pre-cropped to the native 76x52 tile, built into a runtime sprite and set on
// the "cr_letterbox" layer of MapBar.Left.Button.Backgrounds (CRMapBar.xml). See CouncilBarIconSprite for why a loose
// image has to go this road.

namespace CalradiaRemembers.Map
{
   /// <summary>Registers the letter box button picture as the "CR\letterbox_icon" sprite (idempotent).</summary>
   internal static class LetterBoxBarIconSprite
   {
      internal const string SpriteName = @"CR\letterbox_icon";

      private static readonly string IconPath = System.IO.Path.Combine(
         CalradiaRemembers.Paths.ModulePaths.Root, "GUI", "Images", "cr_letterbox_button.png");

      private static bool _successLogged;

      /// <summary>Builds and injects the sprite and repairs the live brush layer; safe to call on every map open.</summary>
      public static void EnsureRegistered() => CouncilBarIconSprite.EnsureLayer(LetterBoxNavigationItemVM.ItemIdValue, SpriteName, IconPath, ref _successLogged);
   }
}
