// Code written by Gabriel Mailhot, 28/07/2026.
// Chat UI skin switcher (dev/test). A skin is a prefab bound to the same ChatViewModel, so this only changes
// the look; it applies to the NEXT conversation (the movie is chosen when the window opens).

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
      ///   cr.skin [classic|manuscript]
      ///   Picks the chat window skin for the next conversation, or (no arg) reports the current one.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("skin", "cr")]
      public static string Skin(List<string> args)
      {
         string name = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "";

         switch (name)
         {
            case "classic":
               ChatManager.SkinPrefab = ChatManager.ClassicSkin;

               return "Chat skin set to Classic. Applies to the next conversation.";
            case "manuscript":
               ChatManager.SkinPrefab = ChatManager.ManuscriptSkin;

               return "Chat skin set to Manuscript. Applies to the next conversation.";
            default:
               return $"Current chat skin: {ChatManager.SkinPrefab}. Usage: cr.skin [classic|manuscript].";
         }
      }
   }
}
