// Code written by Gabriel Mailhot, 07/09/2026.
// THROWAWAY SPIKE (cr.tick_probe). It answers ONE question, and nothing else should be built on it.
//
// Every full-map panel problem this mod has chased (the siege-bombardment native crash, the map reading its own
// hotkeys under a letter the player is typing, the clock we freeze by hand) has the same suspected root cause: CR
// pushes a SCREEN (ScreenManager.PushScreen) while vanilla pushes a STATE. MapScreen.OpenClanScreen does
// GameStateManager.PushState(CreateState<ClanState>()), so the map state stops being active. We only stack a picture
// on top, so MapState stays active and MapScreen keeps receiving OnFrameTick underneath, which is what drives both
// SandBoxViewVisualManager.OnFrameTick (the crash) and TickNavigationInput (the hotkeys).
//
// That is a HYPOTHESIS. Migrating the hosting of two shipped panels on an unverified premise would be a large change
// bought on faith, so this measures it first. The two guards already patch the exact methods in question, so the
// probe needs no new Harmony: it reads their call counters across a fixed window.
//
// The control matters as much as the experiment: the SAME screen class is pushed both ways, so the only variable is
// the mechanism. Expect the counters to keep climbing under PushScreen (proving the probe can see the map ticking)
// and to stop under PushState. Any other result kills the migration, which is exactly what a spike is for.

#region

using System;
using CalradiaRemembers.UI;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.UI.Spike
{
   /// <summary>The spike's game state: deliberately empty, it exists only to be pushed so a screen binds to it.</summary>
   public sealed class CrSpikeState : TaleWorlds.Core.GameState
   {
      /// <summary>False, like the clan and kingdom states: this is a full screen, not a game menu.</summary>
      public override bool IsMenuState => false;
   }

   /// <summary>
   ///   The one screen the probe uses, pushed BOTH ways so the measurement compares mechanisms rather than screens.
   ///   The engine constructs it with the state when it is reached through <see cref="CrSpikeState" /> (that is what
   ///   <c>GameStateScreen</c> does, exactly as GauntletClanScreen takes a ClanState); the parameterless constructor
   ///   is for the PushScreen control. It owns a focused, empty Gauntlet layer so it is a real top screen taking
   ///   input, not a transparent no-op the engine might treat as not covering the map.
   /// </summary>
   [GameStateScreen(typeof(CrSpikeState))]
   internal sealed class CrSpikeScreen : ScreenBase
   {
      private const int LayerPriority = 200;

      private GauntletLayer _layer;

      public CrSpikeScreen() { }

      public CrSpikeScreen(CrSpikeState state) { }

      protected override void OnInitialize()
      {
         base.OnInitialize();

         _layer = new GauntletLayer("CrTickProbe", LayerPriority);
         _layer.InputRestrictions.SetInputRestrictions();
         AddLayer(_layer);
         ScreenManager.TrySetFocus(_layer);
      }

      protected override void OnFinalize()
      {
         if (_layer != null)
         {
            RemoveLayer(_layer);
            _layer = null;
         }

         base.OnFinalize();
      }
   }

   /// <summary>
   ///   Measures whether a way of hosting a panel actually silences the live map underneath it, by sampling the
   ///   guards' call counters across a fixed window. Drive it with <c>cr.tick_probe</c>; the verdict lands in the
   ///   log and on screen.
   /// </summary>
   public static class CrMapTickSpike
   {
      private enum Mode
      {
         /// <summary>Nothing pushed: the baseline that says what a live, unobstructed map ticks at.</summary>
         None,

         /// <summary>The screen pushed the way CR does it today.</summary>
         Screen,

         /// <summary>The same screen reached the way vanilla does it, through a game state.</summary>
         State
      }

      private static bool _armed;
      private static Mode _mode;
      private static float _remaining;
      private static long _visualAtStart;
      private static long _navAtStart;
      private static CrSpikeScreen _pushedScreen;

      /// <summary>True while a measurement window is open, so a second cr.tick_probe cannot corrupt the first.</summary>
      public static bool IsRunning => _armed;

      /// <summary>
      ///   Opens a measurement window: pushes nothing, the screen, or the state, samples the counters, and closes
      ///   itself after <paramref name="seconds" />. Returns the message to show the player.
      /// </summary>
      public static string Start(string mode, float seconds)
      {
         if (_armed) return "A tick probe is already running. Wait for it to report.";

         if (!Enum.TryParse(mode ?? "", true, out Mode parsed))
            return "Usage: cr.tick_probe <none|screen|state> [seconds]. 'none' is the live-map baseline, 'screen' is how CR hosts panels today, 'state' is how vanilla opens the clan screen.";

         if (!SettlementVisualTickGuard.IsPatched)
            return "The siege-visual guard is not attached, so the probe has nothing to count. Nothing measured.";

         _mode = parsed;
         _remaining = seconds <= 0f ? 4f : seconds;
         _visualAtStart = SettlementVisualTickGuard.CallCount;
         _navAtStart = MapNavigationInputGuard.CallCount;

         try
         {
            switch (_mode)
            {
               case Mode.Screen:
                  _pushedScreen = new CrSpikeScreen();
                  ScreenManager.PushScreen(_pushedScreen);

                  break;
               case Mode.State:
                  Game.Current.GameStateManager.PushState(Game.Current.GameStateManager.CreateState<CrSpikeState>());

                  break;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[TICKPROBE] could not open the {_mode} host: {ex.GetType().Name}: {ex.Message}");

            return $"Could not open the {_mode} host: {ex.Message}";
         }

         _armed = true;
         ModLog.Info($"[TICKPROBE] {_mode}: window open for {_remaining:0.#}s (visual={_visualAtStart}, nav={_navAtStart}).");

         return $"Probing the map tick under '{_mode}' for {_remaining:0.#}s. The verdict goes to the log.";
      }

      /// <summary>Drives the open window. Called from the submodule's application tick, which runs whatever owns the screen.</summary>
      public static void Tick(float dt)
      {
         if (!_armed) return;

         _remaining -= dt;
         if (_remaining > 0f) return;

         try
         {
            long visual = SettlementVisualTickGuard.CallCount - _visualAtStart;
            long nav = MapNavigationInputGuard.CallCount - _navAtStart;
            string verdict = visual == 0 && nav == 0
               ? "the map is SILENT under this host"
               : "the map is STILL TICKING under this host";

            string line = $"[TICKPROBE] {_mode}: settlement-visual ticks={visual}, map-input ticks={nav}. Verdict: {verdict}.";
            ModLog.Compat(line); // always recorded: the whole point of the run is to read this afterwards
            InformationManager.DisplayMessage(new InformationMessage(line));
         }
         catch (Exception ex) { ModLog.Error($"[TICKPROBE] reporting failed: {ex.GetType().Name}: {ex.Message}"); }
         finally
         {
            Closehost();
            _armed = false;
            _pushedScreen = null;
         }
      }

      // Puts the screen stack back exactly as it was, whichever way the host was opened.
      private static void Closehost()
      {
         try
         {
            switch (_mode)
            {
               case Mode.Screen:
                  if (ScreenManager.TopScreen == _pushedScreen) ScreenManager.PopScreen();

                  break;
               case Mode.State:
                  if (Game.Current?.GameStateManager?.ActiveState is CrSpikeState) Game.Current.GameStateManager.PopState();

                  break;
            }
         }
         catch (Exception ex) { ModLog.Error($"[TICKPROBE] closing the {_mode} host failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
