// Code written by Gabriel Mailhot, 12/10/2026.
// Lordfadooboo and crazyblox (Nexus, 12/10/2026): on 2.7.8, a new Story Mode game hung on the loading screen of the
// training field, a Sandbox game started fine, and 2.7.7 did not hang. Story Mode puts the player's elder brother in the
// main party (TutorialPhaseCampaignBehavior.InitializeTutorial) and, when character creation is over, enters the
// training field and opens its mission (TrainingFieldCampaignBehavior.OnCharacterCreationIsOver). 2.7.8 let family in
// the party ask for a private word on a settlement entry, so the brother could ask in that very moment. The game
// disables every interaction during its tutorial (MainStoryLine.IsPlayerInteractionRestricted); this reads it for the
// one initiative gate (ActionGate), so no CR character acts while the game says nobody may be talked to.
// CR does not reference StoryMode.dll (it runs without it), so the flag is read by reflection; Sandbox has none.

#region

using System;
using System.Reflection;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Whether the game's Story Mode tutorial restricts the player's interactions right now.</summary>
   internal static class StoryModeTutorial
   {
      private static bool _resolved;
      private static PropertyInfo _current;
      private static PropertyInfo _mainStoryLine;
      private static PropertyInfo _restricted;

      /// <summary>Whether the game's flag was found (StoryMode.dll loaded and the members named as in 1.4.8): for cr.testall.</summary>
      internal static bool FlagFound
      {
         get
         {
            RestrictsInteraction();
            return _current != null && _mainStoryLine != null && _restricted != null;
         }
      }

      /// <summary>True while the Story Mode tutorial restricts interaction; false in Sandbox, after it, or on a read error.</summary>
      internal static bool RestrictsInteraction()
      {
         try
         {
            if (!_resolved)
            {
               // The game loads StoryMode.dll from its module folder, so it is found among the loaded assemblies, not by
               // a name the default load context would resolve. Resolved only once found: an early call before the
               // module loads must not leave the gate blind for the session.
               Type manager = null;
               foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                  if (a.GetName().Name == "StoryMode")
                  {
                     manager = a.GetType("StoryMode.StoryModeManager");
                     break;
                  }

               if (manager == null) return false;

               _resolved = true;
               _current = manager.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
               _mainStoryLine = manager.GetProperty("MainStoryLine", BindingFlags.Public | BindingFlags.Instance);
               _restricted = _mainStoryLine?.PropertyType.GetProperty("IsPlayerInteractionRestricted", BindingFlags.Public | BindingFlags.Instance);
            }

            if (_current == null || _mainStoryLine == null || _restricted == null) return false;

            object current = _current.GetValue(null);
            object line = current == null ? null : _mainStoryLine.GetValue(current);

            return line != null && _restricted.GetValue(line) is true;
         }
         catch
         {
            // Fails OPEN: a broken read must not silence every character for the whole campaign.
            return false;
         }
      }
   }
}
