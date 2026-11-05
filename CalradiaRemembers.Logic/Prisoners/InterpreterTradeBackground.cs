// Code written by Gabriel Mailhot, 05/11/2026.
// RaveN017Mod (Nexus, 05/11/2026): prisoner sales "still don't work every single time; I think it sometimes misinterprets
// things, especially when I perform several prisoner exchanges in a row." The interpreter that turns a reply into a sale read
// that one reply and nothing else: no list of the captives the player holds, no line of the player's, no earlier figure. "I'll
// take him at the same price" named nobody it could see, so the captive and the sum were its guess. Gabriel (05/11/2026)
// approved giving it the captives and the figures named. On a turn that is about captives, it now reads the names it must
// choose from and the exchange just before the reply; every other turn reads exactly what it read before.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Logic.Prisoners
{
   /// <summary>What the interpreter could be told about a trade in captives on this turn.</summary>
   public sealed class InterpreterTradeFacts
   {
      /// <summary>The hero captives the player holds, by the names the game knows them under.</summary>
      public IReadOnlyList<string> HeldCaptives { get; init; }

      /// <summary>The player's line this turn.</summary>
      public string PlayerLine { get; init; }

      /// <summary>The character's reply the turn before, where an agreed figure or a captive's name may have been said.</summary>
      public string PreviousReply { get; init; }

      /// <summary>The reply the interpreter is about to read.</summary>
      public string Reply { get; init; }
   }

   /// <summary>The background the interpreter reads before naming a captive or a price. Pure.</summary>
   public static class InterpreterTradeBackground
   {
      /// <summary>TUNE: the longest a quoted line runs, so a long reply does not crowd the reading prompt.</summary>
      public const int MaxQuotedChars = 600;

      /// <summary>TUNE: the most captive names listed; a full dungeon must not become a page.</summary>
      public const int MaxNamesListed = 20;

      private static readonly string[] TradeWords = {"prisoner", "captive", "ransom"};

      /// <summary>
      ///   True when this turn is about captives: the player holds one, and the player's line, the reply or the reply before names
      ///   one of them, or speaks of prisoners, captives or ransom.
      /// </summary>
      public static bool Applies(InterpreterTradeFacts f)
      {
         if (f?.HeldCaptives == null || f.HeldCaptives.Count == 0) return false;

         string[] texts = {f.PlayerLine, f.Reply, f.PreviousReply};

         return texts.Any(t => !string.IsNullOrWhiteSpace(t)
                               && (f.HeldCaptives.Any(n => NamesAsWords(n, t))
                                   || TradeWords.Any(w => t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)));
      }

      /// <summary>
      ///   The lines added to the interpreter's context on a turn about captives: the names a buy_prisoner target must be one of,
      ///   then the reply before and the player's line as background. Null on any other turn, so its prompt stays as it was.
      /// </summary>
      public static string Block(InterpreterTradeFacts f)
      {
         if (!Applies(f)) return null;

         IEnumerable<string> names = f.HeldCaptives.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Take(MaxNamesListed);
         string captives = "CAPTIVES THE PLAYER HOLDS (a buy_prisoner target is one of these, written exactly as here; for \"him\" or "
                           + "\"her\", the one the conversation below names; the price is the figure agreed there or in the reply): "
                           + string.Join("; ", names) + ".";

         var turns = new List<string>();
         if (!string.IsNullOrWhiteSpace(f.PreviousReply)) turns.Add("YOU (your previous reply): " + Quote(f.PreviousReply));
         if (!string.IsNullOrWhiteSpace(f.PlayerLine)) turns.Add("PLAYER: " + Quote(f.PlayerLine));

         return ActionInterpreterContextBuilder.AppendConversationSoFar(captives, string.Join(Environment.NewLine, turns));
      }

      #region private

      private static string Quote(string text)
      {
         string t = text.Trim();

         return t.Length <= MaxQuotedChars ? t : t.Substring(0, MaxQuotedChars).TrimEnd() + "...";
      }

      // A sentence names a captive only when his name stands in it as whole words: "Ira" is not in "Irawen".
      private static bool NamesAsWords(string name, string text)
      {
         string n = name?.Trim() ?? string.Empty;
         if (n.Length == 0) return false;

         for (int at = text.IndexOf(n, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(n, at + 1, StringComparison.OrdinalIgnoreCase))
         {
            int end = at + n.Length;
            if ((at == 0 || !char.IsLetter(text[at - 1])) && (end == text.Length || !char.IsLetter(text[end]))) return true;
         }

         return false;
      }

      #endregion
   }
}
