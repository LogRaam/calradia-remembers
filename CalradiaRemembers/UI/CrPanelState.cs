// Code written by Gabriel Mailhot, 07/09/2026.
// The payload-carrying GameState behind the panel-hosting migration (see CrPanelHost's header for the measured WHY:
// a pushed SCREEN leaves MapState active and ticking at the same rate as nothing pushed at all, while a pushed
// STATE silences it, exactly like vanilla's ClanState/KingdomState). ONE state class serves every CR panel; what
// distinguishes the letter book from the Balance of Power is only the payload carried here.
//
// The engine constructs the bound screen with THIS state as its sole constructor argument
// (GameStateScreenManager.CreateScreen does Activator.CreateInstance(screenType, state)), so the payload must
// already be set by the time the state is constructed, not assigned afterwards. That is why the useful constructor
// takes every field up front, and is called through GameStateManager.CreateState<CrPanelState>(params), the
// overload that goes through Activator.CreateInstance with those parameters (see CrPanelHost.OpenViaState).

#region

using System;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.UI
{
   /// <summary>
   ///   Carries one panel's hosting payload (prefab, data source, layer sizing) across the push. Never a menu state
   ///   (a full screen, like the vanilla clan/kingdom states).
   /// </summary>
   internal sealed class CrPanelState : TaleWorlds.Core.GameState
   {
      /// <summary>
      ///   Required by GameStateManager.CreateState&lt;T&gt;()'s "where T : GameState, new()" constraint, which C#
      ///   checks at compile time against the type argument regardless of which overload is actually called. Never
      ///   used to construct a real panel state (a payload-less state has nothing for the screen to show); the real
      ///   path always calls the constructor below via CreateState&lt;CrPanelState&gt;(params object[]).
      /// </summary>
      public CrPanelState() { }

      public CrPanelState(string prefabName, ViewModel dataSource, int layerPriority, float designUnitsWidth, float designUnitsHeight, Action onClosed)
      {
         PrefabName = prefabName;
         DataSource = dataSource;
         LayerPriority = layerPriority;
         DesignUnitsWidth = designUnitsWidth;
         DesignUnitsHeight = designUnitsHeight;
         OnClosed = onClosed;
      }

      public string PrefabName { get; }

      public ViewModel DataSource { get; }

      public int LayerPriority { get; }

      public float DesignUnitsWidth { get; }

      public float DesignUnitsHeight { get; }

      /// <summary>
      ///   Fired once the bound screen finalizes, whatever popped it. Carried on the state (not wired after the
      ///   fact) because the screen is constructed by the engine, from this state, before CrPanelHost ever sees the
      ///   screen instance.
      /// </summary>
      public Action OnClosed { get; }

      /// <summary>False, like ClanState/KingdomState: a full screen, not a game menu.</summary>
      public override bool IsMenuState => false;
   }
}
