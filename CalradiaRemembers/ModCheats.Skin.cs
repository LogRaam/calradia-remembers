// Code written by Gabriel Mailhot, 28/07/2026.
// Chat UI skin switcher (dev/test). A skin is a prefab bound to the same ChatViewModel, so this only changes
// the look; it applies to the NEXT conversation (the movie is chosen when the window opens). The normal player
// path is the MCM "Chat Window Style" dropdown; this console command is a DEV OVERRIDE on top of it.

#region

using System.Collections.Generic;
using CalradiaRemembers.Chat;
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
   }
}
