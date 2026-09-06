// Code written by Gabriel Mailhot, 03/09/2026.
// The single crash-safe host every full-map CR panel is composed from, so the safe hosting conditions are
// guaranteed BY CONSTRUCTION and no panel (present or future) can forget one. It exists because the Balance of
// Power panel re-implemented the hosting and missed a step: it pushed a screen (which suspends the map's
// RENDERING, the VRAM-leak-free recipe) but did NOT stop the map's SIMULATION, so the vanilla siege-bombardment
// tick kept running underneath and crashed natively. This host guarantees THREE conditions, always:
//   1. pushes a real ScreenBase (CrPanelScreen)  -> suspends the map render (no render-texture VRAM leak),
//   2. freezes the campaign clock while open      -> stops the map SIMULATION advancing under the panel, and
//   3. suppresses the settlement-visual tick      -> that tick runs on the FRAME loop (not the clock), so the freeze
//      cannot stop its siege-bombardment SetGlobalFrame, which crashes natively under a pushed screen; the
//      SettlementVisualTickGuard Harmony prefix skips it while SuppressSettlementVisualTick is set (see that file).
// A panel becomes a thin composition: a prefab name and a data-source factory. Reload is close+reopen (the proven
// path: repeated in-place movie rebuilds ended in a native access violation). The time freeze is on by default and
// only skipped by an explicit opt-out, so it can never be lost by omission.

#region

using System;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.UI
{
   /// <summary>
   ///   Show/hide lifecycle for one CR panel, composed from a prefab name and a data-source factory. Owns the
   ///   pushed <see cref="CrPanelScreen" />, the campaign-clock freeze, and the reload; a panel manager holds one
   ///   of these and forwards Open/Close/Reload to it. Not thread-safe: call on the main (UI) thread, like every
   ///   ScreenManager operation.
   /// </summary>
   internal sealed class CrPanelHost
   {
      private readonly string _prefabName;
      private readonly Func<Action, ViewModel> _dataSourceFactory;
      private readonly bool _pauseCampaignTime;
      private readonly int _layerPriority;
      private readonly float _designUnitsWidth;
      private readonly float _designUnitsHeight;

      private CrPanelScreen _screen;
      private bool _reloading;

      private CampaignTimeControlMode _previousTimeControlMode;
      private bool _previousTimeControlModeLock;
      private bool _timeControlModeSaved;

      // While ANY CR panel owns the screen, the vanilla settlement-visual tick must be suppressed. That tick
      // (SettlementVisualManager.OnTick -> SettlementVisual.Tick -> the siege-bombardment SetGlobalFrame) runs on
      // the FRAME loop, not the campaign clock, so freezing time cannot stop it; a projectile frozen mid-flight
      // keeps the condition true and crashes natively under a pushed screen (four crashes, offset IL_0aa7). The
      // Harmony prefix in SettlementVisualTickGuard reads the flag below. A reference COUNT, not a bool, so
      // overlapping panels AND the close->reopen reload gap all keep suppression > 0 (a tick slipping through the
      // reload gap is exactly the repeated-cr.reload_ui crash). Its transitions are the pure, unit-tested
      // PanelTickSuppressionCounter.
      private static readonly PanelTickSuppressionCounter VisualTick = new PanelTickSuppressionCounter();

      /// <param name="prefabName">The GauntletUI prefab to host.</param>
      /// <param name="dataSourceFactory">
      ///   Builds a FRESH data source (view model) each open; the argument is the "close this panel" action to wire
      ///   onto the view model (e.g. its OnCloseRequested), so a fresh view model is used on every open/reopen.
      /// </param>
      /// <param name="pauseCampaignTime">
      ///   Freeze the campaign clock while open (default true, the crash-safe choice). Only a panel that genuinely
      ///   must let the map keep simulating should pass false, and it does so explicitly, never by omission.
      /// </param>
      /// <param name="layerPriority">The GauntletLayer priority (default 11000, above the map UI).</param>
      /// <param name="designUnitsWidth">
      ///   Unit-space size the prefab's absolute layout was tuned in (the author's own screen — e.g. 2560x1440
      ///   units for a 4K display at UI-Scale 75%). 0 (default) lays out unscaled. Panels with
      ///   absolutely-positioned data over painted art pass their design size so the layer fit-zooms to the
      ///   player's real layout space (fully visible, never cropped) instead of the data drifting off the art
      ///   (see CrPanelScreen).
      /// </param>
      /// <param name="designUnitsHeight">The height half of the design space; required when designUnitsWidth is set.</param>
      public CrPanelHost(string prefabName, Func<Action, ViewModel> dataSourceFactory, bool pauseCampaignTime = true, int layerPriority = 11000, float designUnitsWidth = 0f, float designUnitsHeight = 0f)
      {
         _prefabName = prefabName;
         _dataSourceFactory = dataSourceFactory;
         _pauseCampaignTime = pauseCampaignTime;
         _layerPriority = layerPriority;
         _designUnitsWidth = designUnitsWidth;
         _designUnitsHeight = designUnitsHeight;
      }

      public bool IsOpen => _screen != null;

      /// <summary>
      ///   True while at least one CR panel owns the screen (open, or mid-reload). The SettlementVisualTickGuard
      ///   Harmony prefix reads this to skip the vanilla siege-visual tick that crashes natively under a pushed
      ///   panel. Static because the guard patches one engine method for all panels, not per instance.
      /// </summary>
      public static bool SuppressSettlementVisualTick => VisualTick.IsSuppressed;

      public void Open()
      {
         if (IsOpen) return;

         ViewModel dataSource = _dataSourceFactory(Close);
         _screen = new CrPanelScreen(_prefabName, dataSource, _layerPriority, _designUnitsWidth, _designUnitsHeight) {OnClosed = ResetState};
         ScreenManager.PushScreen(_screen);
         VisualTick.Enter(); // this panel now owns the screen: suppress the crashing siege-visual tick
         if (_pauseCampaignTime) PauseCampaignTime();

         ModLog.Info($"[PANEL] {_prefabName} opened (pushed screen{(_pauseCampaignTime ? ", clock frozen" : "")}).");
      }

      public void Close()
      {
         if (!IsOpen) return;

         _screen.TearDown();
         if (ScreenManager.TopScreen == _screen) ScreenManager.PopScreen();
         else ModLog.Info($"[PANEL] {_prefabName}: not the top screen, leaving the pop to the engine.");

         ResetState();
         ModLog.Info($"[PANEL] {_prefabName} closed.");
      }

      /// <summary>
      ///   Dev hot-reload (cr.reload_ui): CLOSE + REOPEN, never an in-place movie rebuild. Repeated in-place
      ///   rebuilds of a pushed screen's movie ended in a native access violation; a fresh screen, layer, movie
      ///   and data source carry no dangling state (and re-gather live facts as a bonus). CheckForUpdates has
      ///   already re-read the prefab XML by the time this runs, so the reopen loads against the refreshed prefab.
      /// </summary>
      public void Reload()
      {
         if (!IsOpen) return;

         // Hold suppression and the clock freeze ACROSS the close->reopen gap: the extra suppressor keeps the
         // siege-visual tick skipped even in the instant Close has dropped the panel and Open has not yet re-added
         // it (a tick slipping through that gap is the repeated-cr.reload_ui crash), and _reloading tells ResetState
         // not to resume the clock so the reopen re-freezes without a one-frame catch-up burst.
         _reloading = true;
         VisualTick.Enter();
         try
         {
            Close();
            Open();
            ModLog.Info($"[RELOAD_UI] {_prefabName} reopened on the refreshed prefab.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RELOAD_UI] {_prefabName} reload failed: {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            VisualTick.Exit();
            _reloading = false;
         }
      }

      /// <summary>
      ///   Session-launch reset: drops our reference to any screen from a PRIOR session WITHOUT touching the engine
      ///   object (it belongs to a screen torn down by that session or by the load), so a dead reference can never
      ///   leave <see cref="IsOpen" /> stuck true and block every open this session. Never restores the clock (there
      ///   is no live freeze to undo at launch), it only clears our own flags.
      /// </summary>
      public void ResetForSession()
      {
         _screen = null;
         _timeControlModeSaved = false;
         _reloading = false;
         VisualTick.Reset(); // no panel can be open at launch: clear any residue so the siege visuals are not frozen forever
      }

      // Nulls our reference and resumes the clock. Called from Close AND from the screen's OnClosed, so a pop by
      // any path (Close, Escape, engine) clears IsOpen and never leaves the campaign frozen; idempotent.
      private void ResetState()
      {
         if (!_reloading) ResumeCampaignTime(); // during a reload keep the clock frozen across close->reopen
         if (_screen != null) VisualTick.Exit(); // exactly once per open (an idempotent second call sees a null screen)
         _screen = null;
      }

      // Mirrors ChatManager.PauseCampaignMap: a LOCKED clock ignores a mode change, so release the lock first,
      // remembering both the mode and the lock to restore exactly what we interrupted.
      private void PauseCampaignTime()
      {
         if (Campaign.Current == null) return;

         // Guard the save: on a reload the resume was skipped, so the clock is still our Stop and the ORIGINAL
         // pre-panel mode is what must be restored on the eventual real close. Re-saving here would record Stop as
         // the "previous" mode and strand the player at paused time after a reload.
         if (!_timeControlModeSaved)
         {
            _previousTimeControlMode = Campaign.Current.TimeControlMode;
            _previousTimeControlModeLock = Campaign.Current.TimeControlModeLock;
            _timeControlModeSaved = true;
         }

         Campaign.Current.SetTimeControlModeLock(false);
         Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
      }

      // Idempotent: ResetState runs on every pop path, sometimes twice for one close. Restores both the mode and
      // the lock, so a forced state we interrupted resumes.
      private void ResumeCampaignTime()
      {
         if (!_timeControlModeSaved) return;
         _timeControlModeSaved = false;

         if (Campaign.Current == null) return;

         Campaign.Current.TimeControlMode = _previousTimeControlMode;
         Campaign.Current.SetTimeControlModeLock(_previousTimeControlModeLock);
      }
   }
}
