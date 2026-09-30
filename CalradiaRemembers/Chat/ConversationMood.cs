// Code written by Gabriel Mailhot, 30/09/2026.
// THE MOOD OF THE DAY, increment 4 (ROADMAP, Ruling 4): the player moves a character's mood during a conversation.
// How far, per character, for this conversation only: a new chat window starts from each character's day again ("it
// resets by the next conversation; a mood that persisted would be Stance with extra steps"). The character reports
// each turn on a "mood:" line in its [STANCE] block; MoodLadder clamps it to one band a turn and two a conversation.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Mood;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>How far the player has moved each character's mood in the current conversation.</summary>
   public static class ConversationMood
   {
      private static readonly Dictionary<string, int> Steps = new Dictionary<string, int>();

      /// <summary>A new conversation: every character starts again from their day.</summary>
      public static void Reset() => Steps.Clear();

      /// <summary>Adds one turn's report (+1 lifted, -1 lowered, 0 neither) for this character.</summary>
      public static void Record(string heroId, int turnMood)
      {
         if (string.IsNullOrEmpty(heroId) || turnMood == 0) return;

         Steps.TryGetValue(heroId, out int soFar);
         Steps[heroId] = MoodLadder.AddTurn(soFar, turnMood);
         ModLog.Info($"[MOOD] {heroId}: the exchange {(turnMood > 0 ? "lifted" : "lowered")} the mood (now {Steps[heroId]:+#;-#;0} band(s) from the day's).");
      }

      /// <summary>Where the character's mood stands now, or null when the player has not moved it.</summary>
      public static DayMood? NowFor(string heroId, DayMood? dayMood)
      {
         if (dayMood == null || string.IsNullOrEmpty(heroId) || !Steps.TryGetValue(heroId, out int steps) || steps == 0) return null;

         return MoodLadder.Shift(dayMood.Value, steps);
      }
   }
}
