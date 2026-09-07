// Code written by Gabriel Mailhot, 05/09/2026.
// The tiny data source for the Clan-screen "Balance of Power" button overlay movie (CR_ClanBalanceOfPowerButton.xml).
// It carries only the label and the click command; the button opens the World Almanac dashboard.
//
// 07/09/2026: stopped closing the Clan screen first. That workaround was for a DIFFERENT crash (pushing a bare
// SCREEN over the clan screen left the still-ticking global map bar indexing stale input) that no longer applies now
// that BalanceOfPowerManager pushes a STATE: CrPanelState stacks over ClanState exactly like any other vanilla
// state-over-state layering (inventory over map, etc.), so Clan is simply deactivated underneath, not fought with.

#region

using System;
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
         // Ordinary state sequencing now: BoP's CrPanelState pushes ON TOP of the live ClanState (never popped), the
         // same layering vanilla uses for e.g. inventory-over-map. Clan reactivates on its own once BoP closes.
         try
         {
            BalanceOfPowerManager.Open();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BOP-CLAN] Open() threw: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
