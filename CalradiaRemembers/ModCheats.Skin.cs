// Code written by Gabriel Mailhot, 28/07/2026.
// Chat UI skin switcher (dev/test). A skin is a prefab bound to the same ChatViewModel, so this only changes
// the look; it applies to the NEXT conversation (the movie is chosen when the window opens). The normal player
// path is the MCM "Chat Window Style" dropdown; this console command is a DEV OVERRIDE on top of it.

#region

using System.Collections.Generic;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Letters;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.skin [classic|manuscript|default]
      ///   Overrides the chat window skin for the next conversation (classic/manuscript), or 'default' to clear
      ///   the override and follow the MCM menu choice again. No argument reports the current state.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("skin", "cr")]
      public static string Skin(List<string> args)
      {
         string name = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "";

         switch (name)
         {
            case "classic":
            case "origin":
               ChatManager.SkinConsoleOverride = ChatManager.ClassicSkin;

               return "Chat skin override set to Classic (Origin). Applies to the next conversation.";
            case "manuscript":
            case "leather":
               ChatManager.SkinConsoleOverride = ChatManager.ManuscriptSkin;

               return "Chat skin override set to Manuscript (Dark Leather). Applies to the next conversation.";
            case "default":
            case "mcm":
            case "clear":
               ChatManager.SkinConsoleOverride = null;

               return $"Chat skin override cleared; following the MCM menu ({ModSettings.ChatSkinPrefab}).";
            default:
               string ov = ChatManager.SkinConsoleOverride ?? $"(none, MCM = {ModSettings.ChatSkinPrefab})";

               return $"Chat skin override: {ov}. Currently open with: {ChatManager.ActiveSkin}. " +
                      "Usage: cr.skin [classic|manuscript|default].";
         }
      }

      /// <summary>
      ///   cr.reload_ui
      ///   Dev hot-reload for the chat/memories/letter prefabs: re-reads any changed prefab XML from the DEPLOYED
      ///   module folder and, if the chat OR the letter book is open, rebuilds its widget tree in place (same
      ///   conversation / same letter, same history). Edit the file under the game's own
      ///   Modules\CalradiaRemembers\GUI\Prefabs (NOT the repo copy; a dotnet build would overwrite it), then run
      ///   this to see the change without restarting. NOTE: this reloads XML only, never compiled C#; a change to
      ///   a view-model still needs a full build + game restart.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("reload_ui", "cr")]
      public static string ReloadUi(List<string> args)
      {
         // ChatManager.ReloadActiveMovie runs WidgetFactory.CheckForUpdates first, which re-reads every changed
         // prefab (the letter book included), so the letter reload below then rebuilds against fresh XML.
         ChatManager.ReloadActiveMovie();
         LetterWindowManager.ReloadActiveMovie();

         if (LetterWindowManager.IsOpen)
            return "Prefabs re-read and the open letter book rebuilt in place.";

         return ChatManager.IsOpen
            ? "Prefabs re-read and the open chat rebuilt in place."
            : "Prefabs re-read. Open a chat or a letter to see the changes.";
      }
   }
}
