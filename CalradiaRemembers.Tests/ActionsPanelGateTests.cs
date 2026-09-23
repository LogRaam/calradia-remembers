// Code written by Gabriel Mailhot, 23/09/2026.
// Documents what the Actions tile opens. The new Actions panel exists only in the Classic chat prefab and has not
// been seen in game; the tile exists in every skin, and the default skin (Manuscript, "Dark Leather") has no panel.
// Found while cutting 2.5.22: without this gate, most players' tile would have opened nothing, taking manual action
// injection away from them. Gabriel's call the same day: ship the panel switched off, keep the vanilla menu.

#region

using CalradiaRemembers.Logic.Actions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ActionsPanelGateTests
   {
      // The 2.5.22 state: the panel is off, so every player keeps the menu that works, whatever their skin.
      [Test]
      public void GIVEN_the_panel_switched_off_WHEN_the_tile_is_clicked_in_the_classic_skin_THEN_the_vanilla_menu_opens()
      {
         //Arrange/Act/Assert
         ActionsPanelGate.OpensPanel(panelSwitchedOn: false, skinDrawsPanel: true).Should().BeFalse();
      }

      // The defect found at the cut: a skin with no panel must never be sent to the panel, or the tile does nothing.
      [Test]
      public void GIVEN_the_panel_switched_on_WHEN_the_tile_is_clicked_in_a_skin_without_it_THEN_the_vanilla_menu_opens()
      {
         //Arrange/Act/Assert
         ActionsPanelGate.OpensPanel(panelSwitchedOn: true, skinDrawsPanel: false).Should().BeFalse();
      }

      // Switched on in game to look at it (cr.actions_panel on) in the skin that draws it: the panel opens.
      [Test]
      public void GIVEN_the_panel_switched_on_WHEN_the_tile_is_clicked_in_the_classic_skin_THEN_the_panel_opens()
      {
         //Arrange/Act/Assert
         ActionsPanelGate.OpensPanel(panelSwitchedOn: true, skinDrawsPanel: true).Should().BeTrue();
      }
   }
}
