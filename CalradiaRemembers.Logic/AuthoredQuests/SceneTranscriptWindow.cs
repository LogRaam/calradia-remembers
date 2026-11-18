// Code written by Gabriel Mailhot, 18/11/2026.
// Schmidt (Discord, 18/11/2026): "the quest chat forgets something": that Gunjadrid carried a spear, that the girl hid in
// the log. The narrator was handed only the last 24 lines of the scene, twelve turns; in a scene of 112 turns the spear
// (turns 83 to 95) was gone by turn 109, and Gunjadrid said "I've never carried a spear in my life". Gabriel: "go".
// The scene so far is now kept by a budget of characters: the newest lines whole; past the budget the player's own lines
// stay (short, and what they did is what the story hangs on), the older narration is left out, and the model is told so.
// Pure.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The "scene so far" a narrator reads each turn.</summary>
   public static class SceneTranscriptWindow
   {
      /// <summary>TUNE: about twelve thousand tokens of the scene, newest first (some thirty turns of narration).</summary>
      public const int DefaultBudgetChars = 48000;

      /// <summary>TUNE: the older player lines kept past the budget, at most.</summary>
      public const int OlderPlayerLinesChars = 8000;

      /// <summary>The prefix the chat writes on a player's line.</summary>
      public const string PlayerPrefix = "You: ";

      /// <summary>
      ///   The lines to show, oldest first: every line when they fit; otherwise the player's older lines (the newest of them
      ///   within <see cref="OlderPlayerLinesChars" />), a note that older narration is left out, then the newest lines whole.
      /// </summary>
      public static IReadOnlyList<string> Compose(IReadOnlyList<string> lines, int budgetChars = DefaultBudgetChars)
      {
         if (lines == null || lines.Count == 0) return new List<string>();
         if (lines.Sum(l => (l?.Length ?? 0) + 1) <= budgetChars) return lines.ToList();

         // The newest lines, whole, within the budget (always the last one).
         int start = lines.Count;
         var spent = 0;
         while (start > 0)
         {
            int cost = (lines[start - 1]?.Length ?? 0) + 1;
            if (spent + cost > budgetChars && start < lines.Count) break;

            spent += cost;
            start--;
         }

         // Before them, the player's own lines, newest first within their own budget.
         var older = new List<string>();
         var olderSpent = 0;
         for (int i = start - 1; i >= 0; i--)
         {
            string l = lines[i];
            if (l == null || !l.StartsWith(PlayerPrefix, StringComparison.Ordinal)) continue;
            if (olderSpent + l.Length + 1 > OlderPlayerLinesChars) break;

            olderSpent += l.Length + 1;
            older.Insert(0, l);
         }

         var shown = new List<string>();
         if (older.Count > 0)
         {
            shown.Add("(Earlier in this scene, what the player did; the narration of it is left out here, but it happened:)");
            shown.AddRange(older);
            shown.Add("(Then, in full:)");
         }

         shown.AddRange(lines.Skip(start));

         return shown;
      }
   }
}
