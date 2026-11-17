// Code written by Gabriel Mailhot, 17/11/2026.
// The Story Log, a transcript that "reads like a novel" (MCM, since 06/06/2026), becomes a view of the story archive
// (Gabriel, 17/11/2026: "remplacer le Story log par ce nouveau systeme"). This turns one archive record into the paragraph the
// Story Log file has always carried, and composes the sentences a deed is told in. A deed is told from what the game DID
// (the coin actually moved, the regard actually applied), never from what the model proposed: the archive is a source of
// truth that a character's memory will later read back.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.StoryArchive
{
   /// <summary>Tells archive records as the Story Log's novel-like paragraphs.</summary>
   public static class StoryArchiveNovel
   {
      /// <summary>
      ///   The Story Log paragraph for one record, or null when the novel shows nothing for it (an out-of-character note, a
      ///   refused deed, an empty line). The text carries its own leading line break, as the log always wrote it.
      /// </summary>
      public static string Render(StoryArchiveRecord record)
      {
         if (record == null) return null;

         string nl = Environment.NewLine;
         string text = record.Text?.Trim();
         switch (record.Kind)
         {
            case StoryArchiveKind.EpisodeStart:
               return $"{nl}[Day {(int) record.Time}]{nl}{nl}{MeetingSentence(record)}{nl}";
            case StoryArchiveKind.Line:
               if (string.IsNullOrEmpty(text)) return null;

               return record.IsPlayer ? $"{nl}You: {text}{nl}" : $"{nl}{record.SpeakerName}: {text}{nl}";
            case StoryArchiveKind.Narration:
            case StoryArchiveKind.Notice:
               return string.IsNullOrEmpty(text) ? null : $"{nl}{text}{nl}";
            case StoryArchiveKind.Action:
               return record.Refused || string.IsNullOrEmpty(text) ? null : $"{nl}{text}{nl}";
            case StoryArchiveKind.Letter:
               string heading = record.IsPlayer
                  ? $"--- You send a letter to {record.SpeakerName} (Day {(int) record.Time}) ---"
                  : $"--- Letter from {record.SpeakerName} (Day {(int) record.Time}) ---";

               return $"{nl}{heading}{nl}{nl}{(string.IsNullOrEmpty(text) ? "(The letter is blank.)" : text)}{nl}{nl}---{nl}";
            default:
               return null;
         }
      }

      /// <summary>"You meet Alympia and Martira in the castle of Poros." from an episode's participants and place.</summary>
      public static string MeetingSentence(StoryArchiveRecord episode)
      {
         List<string> names = (episode?.Participants ?? new List<StoryArchiveParticipant>())
                              .Select(p => p?.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
         string who = names.Count == 0 ? "someone"
            : names.Count == 1 ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
         string where = string.IsNullOrWhiteSpace(episode?.PlaceName) ? "" : $" in {episode.PlaceName}";

         return $"You meet {who}{where}.";
      }

      /// <summary>A change of regard the game applied, told as the character's visible reaction.</summary>
      public static string RegardSentence(string name, int delta)
      {
         if (delta == 0) return null;

         return delta >= 3 ? $"{name} is visibly moved by your words."
            : delta >= 1 ? $"{name} seems to appreciate your words."
            : delta >= -2 ? $"{name}'s expression cools slightly."
            : delta >= -5 ? $"{name} takes offense at your words."
            : delta >= -8 ? $"{name}'s regard for you drops sharply."
            : $"A cold distance settles over {name}.";
      }

      /// <summary>Coin the game actually moved: "Lucon hands you 500 denars." or "You hand Lucon 500 denars."</summary>
      public static string CoinSentence(string name, int amount, bool theyGive)
      {
         if (amount <= 0) return null;

         return theyGive ? $"{name} hands you {amount} denars." : $"You hand {name} {amount} denars.";
      }
   }
}
