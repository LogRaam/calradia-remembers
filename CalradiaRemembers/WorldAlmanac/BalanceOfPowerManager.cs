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

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Foundation;
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
      // designUnits: the layout is absolutely positioned over the painted art, tuned on the author's 4K screen at
      // UI-Scale 75% — i.e. a 2560x1440-unit layout space (realPx / (resolution scale x UI scale)). Players with a
      // different resolution/UI-Scale get a smaller unit space, so the whole layer fit-zooms down to keep the
      // panel fully visible and the data glued to the painted slots.
      private static readonly CrPanelHost Host = new CrPanelHost(
         "CalradiaRemembersBalanceOfPower",
         close =>
         {
            List<KingdomPowerFacts> facts = KingdomPowerFactsBuilder.Build();
            PowerIndexResult result = PowerIndexPolicy.Compute(facts);
            ModLog.Info($"Balance of Power: {result.Ranked.Count} kingdoms scored.");

            var vm = new BalanceOfPowerViewModel(result, facts, InGameDateText(), BuildPoliticalMapPath()) {OnCloseRequested = close};
            // Fills the Strategic Assessment: a companion/soldier's async LLM counsel, or an offline summary when the
            // player is alone or the feature is off. Sets a "the council debates.." line now and swaps in the counsel.
            StrategicAssessmentService.Populate(vm, result);

            return vm;
         },
         designUnitsWidth: 2560f, designUnitsHeight: 1440f);

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

      // Builds the political minimap for the current world and returns the path to its cached PNG (empty on failure,
      // so the panel simply shows no map rather than breaking). Cached by a content signature: the same world state
      // reuses the file it already wrote, and a conquest that changes the map writes a new one. Written under the OS
      // temp dir (writable, unlike the game's Program Files module folder).
      private static string BuildPoliticalMapPath()
      {
         try
         {
            uint[,] grid = PoliticalMapFactsBuilder.BuildColorGrid();
            byte[] png = PoliticalMapImageRenderer.RenderPng(grid);
            if (png == null) return "";

            uint signature = PoliticalMapImageRenderer.ComputeSignature(grid);
            // The renderer version rides in the filename: a restyled map must not be masked by a stale PNG an older
            // renderer wrote for the same world state (the signature only hashes the grid, not the paint).
            string path = Path.Combine(Path.GetTempPath(), "CalradiaRemembers",
               $"politicalmap_v{PoliticalMapImageRenderer.RenderVersion}_{signature:x8}.png");

            if (!File.Exists(path))
            {
               Directory.CreateDirectory(Path.GetDirectoryName(path));
               File.WriteAllBytes(path, png);
            }

            return path;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[POLITICAL_MAP] failed to build/cache the map image (panel shows no map): {ex.GetType().Name}: {ex.Message}");

            return "";
         }
      }
   }
}
