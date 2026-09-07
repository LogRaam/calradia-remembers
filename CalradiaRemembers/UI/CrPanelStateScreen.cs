// Code written by Gabriel Mailhot, 07/09/2026.
// The state-bound counterpart of CrPanelScreen (see CrPanelState's header for why one state class, and CrPanelHost's
// header for the measured WHY of the whole migration). Reuses CrPanelScreen's build/teardown recipe by inheritance
// instead of duplicating it: the layer, the movie, the fit-zoom and the release-on-any-pop safety net are all
// unchanged, so a panel hosted this way looks and behaves exactly like one hosted by the older PushScreen strategy.
//
// Two engine facts drive this file's shape, both measured against the live game (2026-09-07):
//   1. GameStateScreenManager.OnCreateState ends with gameState.RegisterListener((val is IGameStateListener) ? val :
//      null), so a state-bound screen that omits IGameStateListener hands the engine a null and the push dies with a
//      NullReferenceException.
//   2. GameStateScreenManager.CreateScreen builds the screen via Activator.CreateInstance(screenType, state), i.e.
//      the ONLY constructor argument is the state, so the payload must already live on CrPanelState.
//
// Teardown is deliberately NOT duplicated into the IGameStateListener hooks: CrPanelScreen.OnFinalize already runs,
// unchanged, when ScreenManager pops this screen (the same automatic safety net the PushScreen strategy has always
// relied on), so CrPanelHost never has to call TearDown by hand for a state-hosted panel (see CrPanelHost.Close).

#region

using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Screens;

#endregion

namespace CalradiaRemembers.UI
{
   /// <summary>
   ///   The screen the engine builds for a pushed <see cref="CrPanelState" />. Carries no logic of its own beyond
   ///   reading the payload off the state in its constructor; every build/teardown behaviour comes from the base
   ///   <see cref="CrPanelScreen" />.
   /// </summary>
   [GameStateScreen(typeof(CrPanelState))]
   internal sealed class CrPanelStateScreen : CrPanelScreen, IGameStateListener
   {
      public CrPanelStateScreen(CrPanelState state)
         : base(state.PrefabName, state.DataSource, state.LayerPriority, state.DesignUnitsWidth, state.DesignUnitsHeight)
      {
         OnClosed = state.OnClosed;
      }

      // The state drives its own lifecycle independently of these (teardown lives in the inherited
      // ScreenBase.OnFinalize, see this file's header); the interface only has to be PRESENT for
      // GameStateScreenManager.OnCreateState to register this screen as the state's listener at all.
      void IGameStateListener.OnActivate() { }

      void IGameStateListener.OnDeactivate() { }

      void IGameStateListener.OnInitialize() { }

      void IGameStateListener.OnFinalize() { }
   }
}
