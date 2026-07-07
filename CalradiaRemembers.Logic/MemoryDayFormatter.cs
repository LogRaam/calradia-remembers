// Code written by Gabriel Mailhot, 07/07/2026.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Renders a memory event's absolute game day as player-readable text: the raw day number plus a
   ///   relative hint ("today", "yesterday", "N days ago"). Day numbers are absolute calendar days (the
   ///   Calradian year is 84 days), which read as gibberish without the relative part (a tester once
   ///   guessed the '9' was the day and the rest hours). Pure so the console (cr.memory) and the in-chat
   ///   memory panel date a memory identically.
   /// </summary>
   public static class MemoryDayFormatter
   {
      /// <summary>
      ///   "Day 91078 (6 days ago)" for a past day; "(today)"/"(yesterday)" for the two most recent; the
      ///   bare "Day N" for a non-positive or future day (nothing sensible to say relative to now).
      /// </summary>
      public static string Describe(int gameDay, int today)
      {
         if (gameDay <= 0 || gameDay > today) return $"Day {gameDay}";

         int ago = today - gameDay;
         string rel = ago == 0
            ? "today"
            : ago == 1
               ? "yesterday"
               : $"{ago} days ago";

         return $"Day {gameDay} ({rel})";
      }
   }
}
