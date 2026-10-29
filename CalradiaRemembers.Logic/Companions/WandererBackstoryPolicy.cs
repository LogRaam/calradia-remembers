// Code written by Gabriel Mailhot, 26/10/2026.
// fearente (Nexus, 26/10/2026): "a companion that I hired a week ago talked like we were of the same clan from childhood
// [...] If there is an easy way to include their vanilla stories to themself when met for the first time it would be
// nice. Some of them have nice stories to build an identity around." The game tells a wanderer's story once, at the first
// meeting (LordConversationsCampaignBehavior: wanderer_prebackstory, then wanderer_introduction_a to _d), and nothing kept
// it. This decides which of the game's lines are that story and how they are kept: one memory of the wanderer's own life,
// in their own words, growing as the story is told and never doubled. Gabriel the same day: "go pour les deux".

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Companions
{
   /// <summary>Which of the game's lines are a wanderer's own story, and the one memory that keeps it.</summary>
   public static class WandererBackstoryPolicy
   {
      /// <summary>How the kept story opens, which is also how it is found again.</summary>
      public const string Opening = "My story, as I told it when we first met:";

      /// <summary>
      ///   How the kept story closes. fearente (Nexus, 29/10/2026): his companion Brave believed the creditor officer of
      ///   his backstory still existed and wanted to settle with him; nothing told the model that the people of the story
      ///   are not in the world. They are remembered and spoken of, never sought.
      /// </summary>
      public const string PastOnly = "(The people, debts and quarrels in this story belong to my past. They are not in Calradia as I travel it now: "
                                   + "I cannot meet them, and I do not set out to find them or settle with them. I may remember them and speak of them.)";

      // The game's own dialogue ids for the story, in order. The generic lines ("I do not care to talk about my past")
      // are no story and are left out.
      private static readonly HashSet<string> StoryLineIds = new(StringComparer.Ordinal) {
         "wanderer_prebackstory", "wanderer_introduction_a", "wanderer_introduction_b", "wanderer_introduction_c", "wanderer_introduction_d"
      };

      /// <summary>True for a line of the wanderer's own story, as the game's dialogue names it.</summary>
      public static bool IsStoryLine(string sentenceId) => sentenceId != null && StoryLineIds.Contains(sentenceId);

      /// <summary>True when this memory is the kept story.</summary>
      public static bool IsTheKeptStory(string summary) => summary != null && summary.StartsWith(Opening, StringComparison.Ordinal);

      /// <summary>
      ///   The kept story with <paramref name="line" /> added: started when there is none, extended in the order told, and
      ///   unchanged when the line is already in it (a story heard twice is not kept twice).
      /// </summary>
      public static string Keep(string keptSoFar, string line)
      {
         string told = line?.Trim();
         if (string.IsNullOrEmpty(told)) return keptSoFar;
         if (!IsTheKeptStory(keptSoFar)) return $"{Opening} \"{told}\" {PastOnly}";
         if (keptSoFar.IndexOf(told, StringComparison.Ordinal) >= 0) return Framed(keptSoFar);

         return $"{Unframed(keptSoFar)} \"{told}\" {PastOnly}";
      }

      /// <summary>
      ///   The kept story closed by <see cref="PastOnly" />, once. A story kept before 2.7.22 has no closing; it is given
      ///   one when profiles are refreshed. Anything that is not the kept story comes back as it was.
      /// </summary>
      public static string Framed(string summary)
         => !IsTheKeptStory(summary) || summary.EndsWith(PastOnly, StringComparison.Ordinal) ? summary : $"{summary.TrimEnd()} {PastOnly}";

      private static string Unframed(string kept)
         => kept.EndsWith(PastOnly, StringComparison.Ordinal) ? kept.Substring(0, kept.Length - PastOnly.Length).TrimEnd() : kept;
   }
}
