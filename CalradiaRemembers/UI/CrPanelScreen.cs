// Code written by Gabriel Mailhot, 03/09/2026.
// The ONE crash-safe host screen every full-map CR panel (Balance of Power, and future World Almanac / persona
// panels) is composed from, so no panel has to re-implement the hosting and none can forget a step. It is the
// generalised form of the old BalanceOfPowerScreen: a real pushed ScreenBase that owns its GauntletLayer and
// movie for a given prefab + data source, sets input restrictions and focus on open, and releases the movie and
// drops the layer on ANY pop (our Close, an Escape, an engine pop). The campaign-clock freeze that stops the map
// SIMULATION crashing under the panel lives in CrPanelHost, which drives this screen; see that file's header.

#region

using System;
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

      private GauntletLayer _layer;
      private GauntletMovieIdentifier _movieId;

      public CrPanelScreen(string prefabName, ViewModel dataSource, int layerPriority)
      {
         _prefabName = prefabName;
         _dataSource = dataSource;
         _layerPriority = layerPriority;
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
         _movieId = _layer.LoadMovie(_prefabName, _dataSource);

         AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);
      }

      /// <summary>
      ///   Releases the movie and drops the layer before the screen is popped. The host pops this screen only
      ///   AFTER calling this and only while it is still the top screen, so nothing is double-released.
      /// </summary>
      public void TearDown() => ReleaseResources();

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

      private void ReleaseResources()
      {
         if (_layer == null) return;

         _layer.IsFocusLayer = false;
         _layer.InputRestrictions.ResetInputRestrictions();
         _layer.ReleaseMovie(_movieId);
         RemoveLayer(_layer);

         _layer = null;
         _movieId = null;
      }
   }
}
