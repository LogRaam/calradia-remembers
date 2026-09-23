// Code written by Gabriel Mailhot, 23/09/2026.
// The Actions panel (fkasad, 22/09/2026) was built blind: its layout has not been seen in game, and only the
// Classic chat prefab draws it. The tile that opens it sits in every skin, and the Manuscript skin (the MCM
// default, "Dark Leather") has no panel to show, so opening the panel there would leave the tile doing nothing
// visible and the player with no way to inject an action by hand. Gabriel, 23/09/2026, cutting 2.5.22: the panel
// ships switched off, and the tile keeps opening the vanilla menu until the panel has been seen in game.

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>Decides whether the Actions tile opens the new panel or the vanilla menu it replaces.</summary>
   public static class ActionsPanelGate
   {
      /// <summary>
      ///   True only when the panel has been switched on AND the open chat skin actually draws it. Otherwise
      ///   the tile opens the vanilla menu, which works in every skin.
      /// </summary>
      public static bool OpensPanel(bool panelSwitchedOn, bool skinDrawsPanel) => panelSwitchedOn && skinDrawsPanel;
   }
}
