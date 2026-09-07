// Code written by Gabriel Mailhot, 03/09/2026.
// The ONE crash-safe host screen every full-map CR panel (Balance of Power, and future World Almanac / persona
// panels) is composed from, so no panel has to re-implement the hosting and none can forget a step. It is the
// generalised form of the old BalanceOfPowerScreen: a real pushed ScreenBase that owns its GauntletLayer and
// movie for a given prefab + data source, sets input restrictions and focus on open, and releases the movie and
// drops the layer on ANY pop (our Close, an Escape, an engine pop). The campaign-clock freeze that stops the map
// SIMULATION crashing under the panel lives in CrPanelHost, which drives this screen; see that file's header.

#region

using System;
using CalradiaRemembers.Logic;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.UI
{
   /// <summary>
   ///   Generic pushed-screen host for a single CR panel prefab. Owns its layer and movie outright (built in
   ///   <see cref="OnInitialize" />), the same recipe as the old per-panel screens, now shared so every panel is
   ///   hosted identically. Pushing a real screen suspends the map's RENDERING below (the leak-free recipe); the
   ///   simulation freeze is <see cref="CrPanelHost" />'s job.
   /// </summary>
   internal sealed class CrPanelScreen : ScreenBase
   {
      private readonly string _prefabName;
      private readonly ViewModel _dataSource;
      private readonly int _layerPriority;
      private readonly float _designUnitsWidth;  // 0 = no fit zoom (panel handles its own sizing)
      private readonly float _designUnitsHeight;

      private GauntletLayer _layer;
      private GauntletMovieIdentifier _movieId;
      private bool _zoomLogged;

      /// <param name="designUnitsWidth">
      ///   The unit-space size the prefab's absolute layout was tuned in (the author's screen: realPx /
      ///   (UIContext.Scale x UI-Scale option) — 2560x1440 units for a 4K author at UI-Scale 75%). 0 disables the
      ///   fit zoom. Otherwise the whole layer is zoomed so the design canvas is always FULLY VISIBLE, never
      ///   cropped (shrunk to fit whichever axis binds), keeping data glued to the painted art (player report
      ///   2026-09-05: bound data sliding off its slots for players with a different UI scale / ratio).
      /// </param>
      /// <param name="designUnitsHeight">The height half of the design space; required when designUnitsWidth is set.</param>
      public CrPanelScreen(string prefabName, ViewModel dataSource, int layerPriority, float designUnitsWidth = 0f, float designUnitsHeight = 0f)
      {
         _prefabName = prefabName;
         _dataSource = dataSource;
         _layerPriority = layerPriority;
         _designUnitsWidth = designUnitsWidth;
         _designUnitsHeight = designUnitsHeight;
      }

      /// <summary>
      ///   Fired once when the screen is finalized, whatever popped it (Close, an Escape, an engine pop). The host
      ///   wires this to reset its own IsOpen state and resume the campaign clock, so a pop that bypassed Close can
      ///   never leave the panel stuck open or the campaign frozen.
      /// </summary>
      public Action OnClosed { get; set; }

      protected override void OnInitialize()
      {
         base.OnInitialize();

         _layer = new GauntletLayer(_prefabName, _layerPriority);
         _layer.InputRestrictions.SetInputRestrictions();

         // A broken/missing prefab can throw here. Unlike the old overlay hosting, a pushed screen is NOT a
         // soft-lock if the movie is blank (Escape still pops it), so we log and carry on with an empty focused
         // layer rather than trapping the player: the host's Close/Escape path tears it down cleanly.
         try { _movieId = _layer.LoadMovie(_prefabName, _dataSource); }
         catch (Exception ex)
         {
            ModLog.Error($"[PANEL] {_prefabName}: LoadMovie failed (empty layer shown, Escape closes it): {ex.GetType().Name}: {ex.Message}");
            _movieId = null;
         }

         AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);
      }

      /// <summary>
      ///   Releases the movie and drops the layer before the screen is popped. The host pops this screen only
      ///   AFTER calling this and only while it is still the top screen, so nothing is double-released.
      /// </summary>
      public void TearDown() => ReleaseResources();

      protected override void OnFrameTick(float dt)
      {
         base.OnFrameTick(dt);
         ApplyFitZoom();
      }

      // GauntletLayer.UpdateLayout resets UIContext.ScaleModifier to the base UI scale every pass (the same reset
      // ChatManager's skin zoom works around), so the zoom is RE-applied every frame. ABSOLUTE (set from
      // ScreenManager.Scale, never read-multiply) so a missed reset can never compound. FIT (Gabriel's spec,
      // 2026-09-05): the whole panel must always be fully visible, never cropped.
      // The available layout space is computed, not guessed: a widget's on-screen size is units x CustomScale,
      // where CustomScale = UIContext.Scale (resolution + the engine's own narrow-aspect shrink) x ScaleModifier
      // (the player's UI-Scale option, reset to ScreenManager.Scale each pass). The space a panel can lay out in
      // is therefore realPx / (Scale x baseScale) units — the panel's author had 2560x1440 units (4K at UI-Scale
      // 75%), a 1080p player at UI-Scale 100% has only 1920x1080, so the design canvas zooms down to fit BOTH
      // axes. Data lives inside the same canvas as the art, so it stays glued to its painted slots; on the
      // author's own screen zoom is exactly 1 (the tuned look). ScreenManager.UsableArea looked like the obvious
      // source for this but is never populated (reads 1x1 — measured 2026-09-05).
      private void ApplyFitZoom()
      {
         if (_designUnitsWidth <= 0f || _designUnitsHeight <= 0f || _layer?.UIContext == null) return;

         float realW = ScreenManager.EngineInterface.RealScreenResolutionWidth;
         float realH = ScreenManager.EngineInterface.RealScreenResolutionHeight;
         float basePxPerUnit = _layer.UIContext.Scale * ScreenManager.Scale;
         if (realW <= 0f || realH <= 0f || basePxPerUnit <= 0f) return;

         float unitW = realW / basePxPerUnit;
         float unitH = realH / basePxPerUnit;
         float zoom = Math.Min(unitW / _designUnitsWidth, unitH / _designUnitsHeight);
         if (zoom > 1f) zoom = 1f;

         // Write ONLY when the value actually changes. On a screen roomier than the book, zoom is 1 and the target
         // equals the base the engine resets to each pass, so the old unconditional assignment rewrote the same
         // number every frame for nothing. A player report of 2026-09-06 (crash on scrolling a letter, plus the
         // campaign notification font turning subtly bolder) came from a 1920x1080 screen at UI Scale 1, where the
         // zoom is 1 and cannot itself resize anything: a per-frame write that dirties the UI context is the only
         // way this path could touch them. Guarded, the whole thing is inert for those players, and still re-applies
         // every frame for the ones it is actually for (GauntletLayer.UpdateLayout resets the modifier each pass, so
         // a real zoom differs from the base and is written back immediately).
         float target = ScreenManager.Scale * zoom;
         if (Math.Abs(_layer.UIContext.ScaleModifier - target) > 0.0001f) _layer.UIContext.ScaleModifier = target;

         if (!_zoomLogged)
         {
            _zoomLogged = true; // once per open: the numbers behind any "the layout shifted" report
            ModLog.Info($"[PANEL] {_prefabName} fit-zoom: real={realW:0}x{realH:0}, contextScale={_layer.UIContext.Scale:0.###}, "
                        + $"uiScale={ScreenManager.Scale:0.###}, space={unitW:0}x{unitH:0}, design={_designUnitsWidth:0}x{_designUnitsHeight:0}, zoom={zoom:0.###}.");
         }
      }

      // Safety net: whatever pops this screen still releases the movie and drops the layer, so a pop that bypassed
      // TearDown cannot leak the render texture. Guarded, so the explicit TearDown-then-pop path does not
      // double-release; OnClosed fires once so the host resets IsOpen and resumes the clock on any pop.
      protected override void OnFinalize()
      {
         base.OnFinalize();
         ReleaseResources();
         OnClosed?.Invoke();
         OnClosed = null;
      }

      // Defensive: after a save-load or campaign-end the engine objects behind these fields may already be dead, so
      // every step is guarded and the fields are ALWAYS nulled (finally), so a failed teardown never leaves a zombie
      // layer that blocks every future open. A data source that owns a portrait render-target (IManagedPortrait) has
      // it freed BEFORE the movie is released, so the tableau's GPU render-target is not orphaned.
      private void ReleaseResources()
      {
         if (_layer == null) return;

         try
         {
            _layer.IsFocusLayer = false;
            _layer.InputRestrictions.ResetInputRestrictions();
            (_dataSource as IManagedPortrait)?.ReleasePortrait();
            _layer.ReleaseMovie(_movieId);
            RemoveLayer(_layer);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PANEL] {_prefabName}: teardown failed (non-fatal, fields cleared anyway): {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _layer = null;
            _movieId = null;
         }
      }
   }
}
