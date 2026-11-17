// Code written by Gabriel Mailhot, 17/11/2026.
// Gabriel (17/11/2026): "Chaque action devrait etre recomposee en texte comme si le recit etait un roman." A deed enters the
// story archive as the sentence of what the game DID: the regard actually applied, the coin actually moved, the line the
// game already showed the player for anything else (ActionOutcome.Message, true to what the bridge did). A deed the game
// refused is kept, marked refused, because a character may remember being asked; the novel never tells it as done. A verb the
// model invented, or a regard change gated to nothing, did not happen and leaves no record.

namespace CalradiaRemembers.Logic.StoryArchive
{
   /// <summary>What the game reported for one deed, as the archive needs it.</summary>
   public sealed class StoryArchiveDeedFacts
   {
      /// <summary>The verb, e.g. "give_gold".</summary>
      public string ActionType { get; init; }

      /// <summary>The name of the character who acted.</summary>
      public string ActorName { get; init; }

      /// <summary>The line the game showed the player for this deed.</summary>
      public string Message { get; init; }

      /// <summary>The regard the game actually applied (change_relation), when it did.</summary>
      public int? AppliedRegard { get; init; }

      /// <summary>The coin the game actually moved (give_gold, take_gold), when it did.</summary>
      public int? AppliedCoin { get; init; }

      /// <summary>The game refused the deed.</summary>
      public bool Refused { get; init; }

      /// <summary>The model invented a verb the game has no executor for.</summary>
      public bool UnknownVerb { get; init; }
   }

   /// <summary>The sentence a deed enters the archive with, and whether it enters at all.</summary>
   public sealed class StoryArchiveDeedEntry
   {
      /// <summary>The deed told as a sentence.</summary>
      public string Text { get; init; }

      /// <summary>The game refused it: it did not happen.</summary>
      public bool Refused { get; init; }
   }

   /// <summary>Turns a deed's outcome into its archive sentence. Pure.</summary>
   public static class StoryArchiveDeed
   {
      /// <summary>The entry for one deed, or null when nothing happened that the story should keep.</summary>
      public static StoryArchiveDeedEntry Entry(StoryArchiveDeedFacts facts)
      {
         if (facts == null || facts.UnknownVerb) return null;

         string type = (facts.ActionType ?? "").Trim().ToLowerInvariant();
         if (!facts.Refused && type == "change_relation")
         {
            string regard = facts.AppliedRegard is int d ? StoryArchiveNovel.RegardSentence(facts.ActorName, d) : null;

            return regard == null ? null : new StoryArchiveDeedEntry {Text = regard};
         }

         if (!facts.Refused && (type == "give_gold" || type == "take_gold"))
         {
            string coin = facts.AppliedCoin is int c ? StoryArchiveNovel.CoinSentence(facts.ActorName, c, type == "give_gold") : null;

            return coin == null ? null : new StoryArchiveDeedEntry {Text = coin};
         }

         string message = facts.Message?.Trim();

         return string.IsNullOrEmpty(message) ? null : new StoryArchiveDeedEntry {Text = message, Refused = facts.Refused};
      }

      /// <summary>The archive kind of a transcript line: an out-of-character note the chat records as the player's is a Note.</summary>
      public static StoryArchiveKind KindOfTranscriptLine(bool isPlayer, bool isNarration, string text)
      {
         if (isNarration) return StoryArchiveKind.Narration;

         return isPlayer && (text ?? "").TrimStart().StartsWith("(Out of character", System.StringComparison.OrdinalIgnoreCase)
            ? StoryArchiveKind.Note
            : StoryArchiveKind.Line;
      }
   }
}
