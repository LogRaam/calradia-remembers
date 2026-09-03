// Code written by Gabriel Mailhot, 29/08/2026.
// Hosts the Balance of Power panel's GauntletLayer as a REAL screen on the ScreenManager stack, the same
// pushed-screen recipe as the council/chat window (see Chat/ChatScreen.cs), instead of an overlay layer added
// over the live campaign map. Pushing a screen makes the engine suspend the screen below (the map's 3D scene
// stops rendering), so leaving this dev panel open for several minutes no longer leaks render-texture VRAM
// (the crash this fixes: TopScreen.AddLayer over the live map is the documented leak class).

#region

using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>
   ///   Pushed-screen host for the Balance of Power panel. Unlike <see cref="Chat.ChatScreen" /> (handed a
   ///   pre-built layer, since the chat still needs skin selection before the movie is loaded), this screen owns
   ///   its GauntletLayer and movie outright: it builds and loads both in <see cref="OnInitialize" />, since the
   ///   panel has a single fixed prefab and no per-open choice to make first.
   /// </summary>
   internal sealed class BalanceOfPowerScreen : ScreenBase
   {
      private const string PrefabName = "CalradiaRemembersBalanceOfPower";

      private readonly BalanceOfPowerViewModel _vm;
      private GauntletLayer _layer;
      private GauntletMovieIdentifier _movieId;

      public BalanceOfPowerScreen(BalanceOfPowerViewModel vm)
      {
         _vm = vm;
      }

      /// <summary>
      ///   Fired once when the screen is finalized, whatever popped it (our own Close, an Escape, or the engine).
      ///   The manager wires this to reset its own IsOpen state, so a pop that did NOT go through Close cannot
      ///   leave the panel stuck "open" and unable to reopen.
      /// </summary>
      public System.Action OnClosed { get; set; }

      protected override void OnInitialize()
      {
         base.OnInitialize();

         _layer = new GauntletLayer(PrefabName, 11000);
         _layer.InputRestrictions.SetInputRestrictions();
         _movieId = _layer.LoadMovie(PrefabName, _vm);

         AddLayer(_layer);
         _layer.IsFocusLayer = true;
         ScreenManager.TrySetFocus(_layer);
      }

      /// <summary>
      ///   Dev hot-reload (cr.reload_ui): re-reads the changed prefab XML and rebuilds the widget tree in place
      ///   against the SAME view-model, so panel positions can be tuned without restarting the game or popping
      ///   this screen. XML only: a view-model change still needs a build.
      /// </summary>
      public void ReloadMovie()
      {
         if (_layer == null) return;

         UIResourceManager.WidgetFactory?.CheckForUpdates();
         _layer.ReleaseMovie(_movieId);
         _movieId = _layer.LoadMovie(PrefabName, _vm);
      }

      /// <summary>
      ///   Releases the movie and drops the layer before the screen is popped. Mirrors
      ///   <c>ChatManager.KillChatWindow</c>'s teardown order: the caller pops this screen only AFTER calling
      ///   this, and only while it is still the top screen, so nothing is double-released.
      /// </summary>
      public void TearDown() => ReleaseResources();

      // Safety net: whatever pops this screen (our own Close, an Escape, an engine-driven pop) still releases the
      // movie and layer, so a pop that bypassed TearDown can never leak the render texture this whole change
      // exists to stop. Guarded, so the explicit TearDown-then-pop path does not double-release; the OnClosed
      // callback fires once so the manager's IsOpen state is reset on any pop.
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
