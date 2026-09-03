// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac Inc 3, the show/hide lifecycle for the "Balance of Power" panel: gathers the live per-kingdom
// facts, scores them, and opens the prefab. Hosted as a PUSHED SCREEN (BalanceOfPowerScreen), the same
// leak-free recipe the council/chat window uses (see Chat/ChatScreen.cs): pushing a real screen makes the
// engine suspend the live campaign map below, instead of layering the panel over the top screen (which leaks
// VRAM the longer it stays open, see project_council_hosting_leak). Opened from cr.balanceofpower's sibling
// command cr.powerindex for now (quick visual iteration).
// 29/08/2026: converted from a TopScreen overlay layer to this pushed-screen host after Gabriel left the panel
// open for several minutes and the game crashed (the documented map-overlay VRAM leak).

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Show/hide lifecycle for the Balance of Power window.</summary>
   public static class BalanceOfPowerManager
   {
      private static BalanceOfPowerScreen _screen;
      private static BalanceOfPowerViewModel _vm;

      public static bool IsOpen => _screen != null;

      public static void Close()
      {
         if (!IsOpen) return;

         _screen.TearDown();
         if (ScreenManager.TopScreen == _screen) ScreenManager.PopScreen();
         else ModLog.Info("BalanceOfPowerManager.Close: our screen is not on top, leaving the pop to the engine.");

         ResetState();

         ModLog.Info("Balance of Power window closed.");
      }

      // Nulls our references. Called both from Close and from the screen's OnClosed callback, so a pop by any
      // path (our Close, an Escape, an engine pop) clears IsOpen; idempotent, so being called twice is harmless.
      private static void ResetState()
      {
         _screen = null;
         _vm = null;
      }

      public static void Open()
      {
         if (IsOpen) return;

         List<KingdomPowerFacts> facts = KingdomPowerFactsBuilder.Build();
         PowerIndexResult result = PowerIndexPolicy.Compute(facts);
         _vm = new BalanceOfPowerViewModel(result, facts) {OnCloseRequested = Close};

         // A pushed screen suspends the map below on its own (same as the council/chat pushed-screen mode), so
         // no separate MapSceneSuspender call is needed here. OnClosed resets our state whatever pops the screen
         // (our Close, an Escape, an engine pop), so a bypass of Close can never leave IsOpen stuck true.
         _screen = new BalanceOfPowerScreen(_vm) {OnClosed = ResetState};
         ScreenManager.PushScreen(_screen);

         ModLog.Info($"Balance of Power window opened as a pushed screen ({result.Ranked.Count} kingdoms).");
      }

      /// <summary>
      ///   Dev hot-reload (cr.reload_ui): re-reads the changed prefab XML and, if the panel is open, reopens it —
      ///   so panel positions can be tuned without restarting the game. Edit the prefab under the game's own
      ///   Modules\CalradiaRemembers\GUI\Prefabs (a dotnet build overwrites it from source), then run cr.reload_ui.
      ///   XML only: a view-model change still needs a build.
      ///   <para>
      ///     CLOSE + REOPEN, not an in-place ReleaseMovie/LoadMovie: repeated in-place rebuilds of this pushed
      ///     screen's movie ended in a native access violation (Gabriel, 2026-09-03 — seven cr.reload_ui cycles
      ///     while iterating on the panel's layout, then a 0xC0000005 inside TaleWorlds.Native during render).
      ///     The open/close lifecycle is the proven path: a fresh VM, layer and movie carry no dangling state
      ///     from the rebuild, and the facts are re-gathered as a bonus. The chat and letter overlays keep their
      ///     in-place rebuild, which has never crashed.
      ///   </para>
      /// </summary>
      public static void ReloadActiveMovie()
      {
         if (!IsOpen) return;

         try
         {
            // CheckForUpdates already ran at the start of cr.reload_ui (ChatManager.ReloadActiveMovie), so the
            // reopened movie loads against the refreshed prefab.
            Close();
            Open();
            ModLog.Info("[RELOAD_UI] Balance of Power reopened on the refreshed prefab.");
         }
         catch (System.Exception ex)
         {
            ModLog.Error($"[RELOAD_UI] Balance of Power reload failed: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
