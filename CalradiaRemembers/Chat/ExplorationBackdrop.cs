// Code written by Gabriel Mailhot, 04/10/2026.
// Where a written tale's exploration scene is hosted. Gabriel's play test (04/10/2026): closing the scene in the wood by
// Atrion crashed the game natively (0xC0000005, no managed frame), the second the map scene resumed. Every ordinary
// conversation opens over the game's own map conversation; the scene opened over the bare, live campaign map (the log's
// backdrop was "SceneLayer" alone), which is the hosting CLAUDE.md forbids ("never a Gauntlet layer over the live map"),
// and the family of the native crashes seen under the Balance of Power panel. A scene has no character in 3D, so it is
// hosted like the letters: CrPanelHost pushes a GameState, which deactivates MapState by the engine's own contract,
// with the clock frozen and the settlement-visual tick suppressed; the chat window then opens on that state's screen.
// After the scene the clock is left stopped: the player was fast-forwarding through nights of watch, and a popup with
// the scene's close follows.

#region

using CalradiaRemembers.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>The suspended-map backdrop an exploration scene's chat window sits on.</summary>
   internal static class ExplorationBackdrop
   {
      // Below the chat window's layer (ChatManager's 10000): the backdrop is only the night it sits on.
      private static readonly CrPanelHost Host = new CrPanelHost("CalradiaRemembersSceneBackdrop", _ => new SceneBackdropViewModel(), layerPriority: 100);

      public static bool IsOpen => Host.IsOpen;

      /// <summary>Suspends the map under a dark backdrop. The chat opened next lands on its screen.</summary>
      public static void Open() => Host.Open();

      /// <summary>Gives the map back, and leaves the clock stopped.</summary>
      public static void Close()
      {
         if (!Host.IsOpen) return;
         Host.Close();
         if (Campaign.Current != null) Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
      }

      private sealed class SceneBackdropViewModel : ViewModel
      {
      }
   }
}
