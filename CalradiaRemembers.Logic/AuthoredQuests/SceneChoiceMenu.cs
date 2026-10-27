// Code written by Gabriel Mailhot, 04/10/2026.
// Branching tales: the choices of a scene shown like a numbered menu (Gabriel, 04/10/2026: "like an LLM does in the
// CLI: Choice 1, Choice 2, Chat about that"). Under the narration, the open ways out are listed by number, then
// "Something else...". A click on a way sends its words as the player's line, and typing its number alone is the same;
// the narrator then tells it and writes the marker, and only the marker counts (the game decides, never the click).
// "Something else..." hands the player the input to write freely, and the scene goes on as before. The list shows
// only under a narration the player has not yet answered, and never a closed way.
// 26/10/2026 (Gabriel: "j'ai choisi '3. Something else'... et le LLM recoit '3'"): the number of "Something else..."
// typed alone is the same as clicking it; nothing is sent, the input waits for the player's own words. Then ("si je
// fais something else, on ne devrait jamais revenir aux choix dans cette scene"): once the player has chosen
// "Something else..." or written their own words, the list never comes back in that scene.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>What the chat knows when it asks whether, and what, to list under the narration.</summary>
   public sealed class SceneChoiceMenuFacts
   {
      /// <summary>The words of each way out open now, names filled, in the file's order. Empty: nothing to list.</summary>
      public IReadOnlyList<string> OpenWays { get; init; } = new List<string>();

      /// <summary>
      ///   The last thing in the scene is the narrator's: the player has not answered it yet. False once they write (the
      ///   list disappears), true again under the next narration.
      /// </summary>
      public bool NarrationIsLast { get; init; }

      /// <summary>A turn is in flight (the narrator is writing): nothing can be chosen until it lands.</summary>
      public bool Waiting { get; init; }

      /// <summary>The scene has ended (a way was taken, the night was lost): nothing more to choose.</summary>
      public bool Over { get; init; }

      /// <summary>
      ///   The player chose "Something else..." or wrote their own words in this scene: the scene is theirs to play in
      ///   words from then on, and the list stays gone.
      /// </summary>
      public bool WroteFreely { get; init; }
   }

   /// <summary>One line of the list.</summary>
   public sealed class SceneChoiceMenuItem
   {
      /// <summary>What the line shows: "1. Take the left path", or "3. Something else...".</summary>
      public string Text { get; init; }

      /// <summary>What a click sends as the player's line: the way's words, or null for "Something else...".</summary>
      public string Sends { get; init; }

      /// <summary>True for "Something else...": the click puts the cursor in the input instead of sending.</summary>
      public bool WriteFreely => Sends == null;
   }

   /// <summary>Decides what the numbered list holds, and what a typed number means. Pure.</summary>
   public static class SceneChoiceMenu
   {
      public const string SomethingElse = "Something else...";

      private static readonly Regex NumberAlone = new(@"^\s*(\d{1,3})\s*[.)]?\s*$", RegexOptions.Compiled);

      /// <summary>The lines to show under the narration now; none when the list should not be there.</summary>
      public static IReadOnlyList<SceneChoiceMenuItem> Items(SceneChoiceMenuFacts f)
      {
         List<string> ways = (f?.OpenWays ?? new List<string>()).Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
         if (f == null || ways.Count == 0 || !f.NarrationIsLast || f.Waiting || f.Over || f.WroteFreely) return new List<SceneChoiceMenuItem>();

         var items = ways.Select((w, i) => new SceneChoiceMenuItem {Text = $"{i + 1}. {w}", Sends = w}).ToList();
         items.Add(new SceneChoiceMenuItem {Text = $"{ways.Count + 1}. {SomethingElse}"});

         return items;
      }

      /// <summary>
      ///   What the player's typed line says: a number alone ("1", "1." or "1)") picks that way of the list shown, and
      ///   sends its words; the number of "Something else..." sends nothing (null: write freely); anything else, a number
      ///   with no line behind it included, is free text and goes as typed.
      /// </summary>
      public static string Read(string typed, SceneChoiceMenuFacts f)
      {
         Match m = NumberAlone.Match(typed ?? "");
         if (!m.Success) return typed;

         IReadOnlyList<SceneChoiceMenuItem> items = Items(f);
         int n = int.Parse(m.Groups[1].Value);

         return n >= 1 && n <= items.Count ? items[n - 1].Sends : typed;
      }
   }
}
