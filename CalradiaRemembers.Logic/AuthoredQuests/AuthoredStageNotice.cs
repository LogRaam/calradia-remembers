// Code written by Gabriel Mailhot, 04/10/2026.
// The line that tells the player a written tale has moved on (Gabriel, 04/10/2026: during a quest, a message should
// say a stage is complete and a new stage begins). The game shows it on screen, and in the chat when one is open.

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>The player-facing line for a tale moving to its next stage.</summary>
   public static class AuthoredStageNotice
   {
      /// <summary>
      ///   "Tale: {title}. Step complete. New step: {journal}." with the new stage's journal line (names filled), or
      ///   "A new step begins." when the stage has none.
      /// </summary>
      public static string Text(string title, string nextJournal)
      {
         string head = $"Tale: {title?.Trim()}. Step complete.";
         string line = nextJournal?.Trim();
         if (string.IsNullOrEmpty(line)) return head + " A new step begins.";

         char last = line[line.Length - 1];
         if (last != '.' && last != '!' && last != '?') line += ".";

         return $"{head} New step: {line}";
      }
   }
}
