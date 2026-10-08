// Code written by Gabriel Mailhot, 07/10/2026.
// Rules from tashmetu's tester notes on 2.7.3 (Nexus, 07/10/2026): a choice id the interpreter wrote with the list's
// word glued on ("choice_voice" for "voice"), and a finished tale left open in the journal beside its closed entry.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>Reads back the id of a choice by words the interpreter reported. Pure.</summary>
   public static class SaidChoiceId
   {
      /// <summary>
      ///   The open choice the reported id means, or null: matched as written, then without quotes, case or a leading
      ///   "choice" word (as "choice_voice", "choice: voice", "Choice Voice"). Never stretched to another choice.
      /// </summary>
      public static string Match(string reported, IReadOnlyList<string> openIds)
      {
         if (string.IsNullOrWhiteSpace(reported) || openIds == null || openIds.Count == 0) return null;

         string id = reported.Trim().Trim('"', '\'', '`', '<', '>').Trim();
         string exact = openIds.FirstOrDefault(o => string.Equals(o, id, StringComparison.OrdinalIgnoreCase));
         if (exact != null) return exact;

         foreach (string prefix in new[] {"choice_", "choice:", "choice-", "choice "})
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
               string rest = id.Substring(prefix.Length).Trim();
               string found = openIds.FirstOrDefault(o => string.Equals(o, rest, StringComparison.OrdinalIgnoreCase)
                                                          || string.Equals(o.Replace('_', ' '), rest, StringComparison.OrdinalIgnoreCase));
               if (found != null) return found;
            }

         return null;
      }
   }

   /// <summary>What becomes of a tale's journal entry.</summary>
   public enum TaleJournalVerdict
   {
      Keep,
      CloseCompleted,
      CloseFailed,
      CloseCancelled
   }

   /// <summary>One open journal entry of a tale, against the tale the game is running.</summary>
   public sealed class TaleJournalFacts
   {
      /// <summary>The tale the entry mirrors.</summary>
      public string EntryStoryId { get; init; }

      /// <summary>The newest open entry of that tale (an older one for the same tale is a stray copy).</summary>
      public bool IsNewestOfItsStory { get; init; }

      /// <summary>The tale the game is running, or null.</summary>
      public string RunQuestId { get; init; }

      /// <summary>How that tale stands.</summary>
      public AuthoredQuestStatus? RunStatus { get; init; }
   }

   /// <summary>
   ///   tashmetu (07/10/2026): "a finished tale stays listed twice, under Old Quests and still under Active Quests". Only
   ///   the tale under way keeps one open entry; any other open entry of a tale is closed the way that tale ended. Pure.
   /// </summary>
   public static class TaleJournalPolicy
   {
      public static TaleJournalVerdict Verdict(TaleJournalFacts f)
      {
         if (f == null || string.IsNullOrEmpty(f.EntryStoryId) || f.EntryStoryId != f.RunQuestId) return TaleJournalVerdict.CloseCancelled;

         return f.RunStatus switch {
            AuthoredQuestStatus.Active => f.IsNewestOfItsStory ? TaleJournalVerdict.Keep : TaleJournalVerdict.CloseCancelled,
            AuthoredQuestStatus.Completed => TaleJournalVerdict.CloseCompleted,
            AuthoredQuestStatus.Failed => TaleJournalVerdict.CloseFailed,
            _ => TaleJournalVerdict.CloseCancelled
         };
      }
   }
}
