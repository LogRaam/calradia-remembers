// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac Inc 3, the show/hide lifecycle for the "Balance of Power" panel: gathers the live per-kingdom
// facts, scores them, and shows the prefab. First client of the shared crash-safe host (CalradiaRemembers.UI's
// CrPanelHost), which guarantees BOTH hosting conditions by construction: a pushed screen (suspends the map's
// rendering, no render-texture VRAM leak) AND a frozen campaign clock while open (stops the map SIMULATION, so
// the vanilla siege-bombardment tick can no longer crash natively under the panel). This manager is now just the
// panel's identity: its prefab and a factory for its view model. Opened from cr.balanceofpower / cr.powerindex.
// 29/08/2026: converted from a TopScreen overlay layer to a pushed screen (the map-overlay VRAM leak).
// 03/09/2026: campaign clock freeze added (the siege-under-panel native crash), then the whole pushed-screen +
// freeze + close/reopen-reload recipe was lifted into CrPanelHost so every panel is hosted the same, safe way.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using CalradiaRemembers.UI;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Show/hide lifecycle for the Balance of Power window, composed on the shared crash-safe panel host.</summary>
   public static class BalanceOfPowerManager
   {
      // The panel's identity: its prefab, and a factory that builds a FRESH view model each open (so a reopen
      // re-gathers live facts). The host owns everything else, the pushed screen, the campaign-clock freeze, the
      // input restrictions and focus, teardown on any pop, and the close+reopen hot-reload.
      private static readonly CrPanelHost Host = new CrPanelHost(
         "CalradiaRemembersBalanceOfPower",
         close =>
         {
            List<KingdomPowerFacts> facts = KingdomPowerFactsBuilder.Build();
            PowerIndexResult result = PowerIndexPolicy.Compute(facts);
            ModLog.Info($"Balance of Power: {result.Ranked.Count} kingdoms scored.");

            return new BalanceOfPowerViewModel(result, facts, InGameDateText()) {OnCloseRequested = close};
         });

      public static bool IsOpen => Host.IsOpen;

      public static void Open() => Host.Open();

      public static void Close() => Host.Close();

      /// <summary>Dev hot-reload (cr.reload_ui): close+reopen on the refreshed prefab. See CrPanelHost.Reload.</summary>
      public static void ReloadActiveMovie() => Host.Reload();

      /// <summary>The in-game date line under the painted title: "Day 12 of Spring, 1092".</summary>
      private static string InGameDateText()
      {
         CampaignTime now = CampaignTime.Now;

         return $"Day {now.GetDayOfSeason} of {now.GetSeasonOfYear}, {now.GetYear}";
      }
   }
}
