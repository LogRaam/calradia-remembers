// Code written by Gabriel Mailhot, 05/09/2026.
// The tiny data source for the Clan-screen "Balance of Power" button overlay movie (CR_ClanBalanceOfPowerButton.xml).
// It carries only the label and the click command; the button opens the World Almanac dashboard.

#region

using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Data source for the Clan-screen Balance of Power button overlay: its label and open command.</summary>
   public sealed class ClanBalanceOfPowerButtonVM : ViewModel
   {
      [DataSourceProperty]
      public string BalanceOfPowerButtonText => "Balance of Power";

      // No attribute needed: Gauntlet binds Command.Click to a VM method by name (as vanilla VMs do). DataSourceMethod
      // is a UIExtenderEx mixin attribute, not required on a plain ViewModel.
      public void ExecuteOpenBalanceOfPower()
      {
         ModLog.Info("[BOP-CLAN] button clicked (overlay movie).");

         // Close the Clan screen FIRST, then open BoP over the map. Opening BoP directly over the clan screen crashed
         // natively in the map-bar global layer's panel-switching input (ArgumentOutOfRangeException): pushing a screen
         // while the clan game state is active leaves the still-ticking global map bar indexing a stale input state.
         // Popping to the map is the proven-good context (exactly what cr.balanceofpower opens into). Same call the
         // clan Done button makes: GameStateManager.PopState(0).
         try
         {
            Game.Current?.GameStateManager?.PopState(0);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BOP-CLAN] closing the clan screen failed: {ex.GetType().Name}: {ex.Message}");
         }

         try
         {
            BalanceOfPowerManager.Open();
            ModLog.Info("[BOP-CLAN] Open() returned (IsOpen=" + BalanceOfPowerManager.IsOpen + ").");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BOP-CLAN] Open() threw: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
