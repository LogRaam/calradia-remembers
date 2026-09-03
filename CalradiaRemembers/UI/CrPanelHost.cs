// Code written by Gabriel Mailhot, 03/09/2026.
// The single crash-safe host every full-map CR panel is composed from, so the safe hosting conditions are
// guaranteed BY CONSTRUCTION and no panel (present or future) can forget one. It exists because the Balance of
// Power panel re-implemented the hosting and missed a step: it pushed a screen (which suspends the map's
// RENDERING, the VRAM-leak-free recipe) but did NOT stop the map's SIMULATION, so the vanilla siege-bombardment
// tick kept running underneath and crashed natively three times in one day. This host does BOTH, always:
//   1. pushes a real ScreenBase (CrPanelScreen)  -> suspends the map render (no render-texture VRAM leak), and
//   2. freezes the campaign clock while open      -> stops the map simulation (no vanilla map code crashing under us).
// A panel becomes a thin composition: a prefab name and a data-source factory. Reload is close+reopen (the proven
// path: repeated in-place movie rebuilds ended in a native access violation). The time freeze is on by default and
// only skipped by an explicit opt-out, so it can never be lost by omission.

#region

using System;
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

      private CrPanelScreen _screen;

      private CampaignTimeControlMode _previousTimeControlMode;
      private bool _previousTimeControlModeLock;
      private bool _timeControlModeSaved;

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
      public CrPanelHost(string prefabName, Func<Action, ViewModel> dataSourceFactory, bool pauseCampaignTime = true, int layerPriority = 11000)
      {
         _prefabName = prefabName;
         _dataSourceFactory = dataSourceFactory;
         _pauseCampaignTime = pauseCampaignTime;
         _layerPriority = layerPriority;
      }

      public bool IsOpen => _screen != null;

      public void Open()
      {
         if (IsOpen) return;

         ViewModel dataSource = _dataSourceFactory(Close);
         _screen = new CrPanelScreen(_prefabName, dataSource, _layerPriority) {OnClosed = ResetState};
         ScreenManager.PushScreen(_screen);
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
      }

      // Nulls our reference and resumes the clock. Called from Close AND from the screen's OnClosed, so a pop by
      // any path (Close, Escape, engine) clears IsOpen and never leaves the campaign frozen; idempotent.
      private void ResetState()
      {
         ResumeCampaignTime();
         _screen = null;
      }

      // Mirrors ChatManager.PauseCampaignMap: a LOCKED clock ignores a mode change, so release the lock first,
      // remembering both the mode and the lock to restore exactly what we interrupted.
      private void PauseCampaignTime()
      {
         if (Campaign.Current == null) return;

         _previousTimeControlMode = Campaign.Current.TimeControlMode;
         _previousTimeControlModeLock = Campaign.Current.TimeControlModeLock;
         _timeControlModeSaved = true;
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
