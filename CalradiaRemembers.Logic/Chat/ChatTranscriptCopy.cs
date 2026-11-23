// Code written by Gabriel Mailhot, 22/11/2026.
// The conversation as plain text, for the clipboard. whiskeypatt (Discord 21/11/2026): "Im talking to someone, and they
// reply, I cannot copy what they wrote (UI wont let me)". Gauntlet text cannot be selected. Gabriel (22/11/2026): "oui,
// Ctrl+Shift+C" (Ctrl+C alone belongs to the input field, which copies its own selected text, EditableTextWidget 1.4.8).

#region

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>The keys held this frame, for the copy shortcut.</summary>
   public sealed class ChatCopyKeys
   {
      /// <summary>Either Ctrl key is held.</summary>
      public bool Control { get; init; }

      /// <summary>Either Shift key is held.</summary>
      public bool Shift { get; init; }

      /// <summary>C was pressed this frame.</summary>
      public bool CPressed { get; init; }
   }

   /// <summary>Turns the chat window's message blocks into plain text a player can paste anywhere. Pure.</summary>
   public static class ChatTranscriptCopy
   {
      private static readonly Regex Tag = new Regex(@"<[^<>\r\n]{1,200}>", RegexOptions.CultureInvariant);

      /// <summary>
      ///   One block per message, as the window shows them ("Author\ntext"), becomes "Author: text" with the window's
      ///   rich-text tags removed, blocks separated by a blank line. A block with no author line stays as its text. Empty
      ///   blocks are skipped; null or nothing gives an empty string.
      /// </summary>
      public static string Compose(IEnumerable<string> blocks)
      {
         if (blocks == null) return "";

         var sb = new StringBuilder();
         foreach (string block in blocks)
         {
            string plain = Tag.Replace(block ?? "", "").Replace("\r\n", "\n").Trim();
            if (plain.Length == 0) continue;

            int newline = plain.IndexOf('\n');
            string line = newline > 0 ? $"{plain.Substring(0, newline).Trim()}: {plain.Substring(newline + 1).Trim()}" : plain;

            if (sb.Length > 0) sb.Append("\r\n\r\n");
            sb.Append(line.Replace("\n", "\r\n"));
         }

         return sb.ToString();
      }

      /// <summary>The shortcut: Ctrl and Shift held (either side) as C is pressed.</summary>
      public static bool IsShortcut(ChatCopyKeys keys) => keys != null && keys.Control && keys.Shift && keys.CPressed;
   }
}
