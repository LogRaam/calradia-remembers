// Code written by Gabriel Mailhot, 17/07/2026.
// Feedback loop (quest audit C5): a transient, per-hero note that a [QUEST] block the model emitted last turn
// could not be registered. QuestService records it at the point of refusal; EncounterContextBuilder consumes it
// into the NEXT turn's prompt (see EncounterContext.LlmFormatFeedbackNote), so the MODEL learns of the refusal,
// not just the player, and stops referencing a task the game never recorded. Deliberately NOT persisted: it is a
// one-turn nudge, meaningless in a save.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>In-memory, per-hero pending feedback about a block the host refused last turn. Consumed once.</summary>
   internal static class LlmFeedbackNotes
   {
      private static readonly Dictionary<string, string> Pending = new();

      /// <summary>Records (replacing) the note for a hero; the next prompt build for them will surface it.</summary>
      internal static void Set(string heroId, string note)
      {
         if (string.IsNullOrEmpty(heroId) || string.IsNullOrWhiteSpace(note)) return;

         Pending[heroId] = note;
      }

      /// <summary>Returns and CLEARS the pending note for a hero, or null when there is none, so it nudges once.</summary>
      internal static string ConsumeFor(string heroId)
      {
         if (string.IsNullOrEmpty(heroId) || !Pending.TryGetValue(heroId, out string note)) return null;

         Pending.Remove(heroId);

         return note;
      }
   }
}
