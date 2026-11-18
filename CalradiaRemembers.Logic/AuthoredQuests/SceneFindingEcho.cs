// Code written by Gabriel Mailhot, 18/11/2026.
// Schmidt (Discord, 18/11/2026, The Wolf of the Wood on DeepSeek V4.1 Flash): "I feel like the quest is never ending". The
// narrator described the wolf pelt on its frame over three turns, the polished stones in its eyes and all, and never wrote
// [[found:pelt]]; the scene ends only once every required finding is found, so it ran 112 turns while the narrator
// improvised the rest of the tale. Gabriel: "go". A finding the narration plainly shows counts as found even when the
// marker is forgotten.
// Pure: the host hands the narration, the findings still to find (names filled) and the scene's own opening words.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.StoryArchive;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>A finding still to be found, as the narrator was told it.</summary>
   public sealed class SceneFindingText
   {
      /// <summary>The finding's id (its marker).</summary>
      public string Id { get; init; }

      /// <summary>Its text, names filled.</summary>
      public string Text { get; init; }
   }

   /// <summary>What one narration is checked against.</summary>
   public sealed class SceneFindingEchoFacts
   {
      /// <summary>The narration, markers taken out.</summary>
      public string Narration { get; init; }

      /// <summary>The findings not yet found.</summary>
      public IReadOnlyList<SceneFindingText> Left { get; init; }

      /// <summary>The scene's opening canvas and facts: words every narration of the place uses, which prove nothing.</summary>
      public string SceneWords { get; init; }
   }

   /// <summary>Finds the findings a narration shows without having written their marker. Pure.</summary>
   public static class SceneFindingEcho
   {
      /// <summary>TUNE: at least this many of a finding's own words must appear in the narration.</summary>
      public const int MinWords = 2;

      /// <summary>TUNE: and at least this share of them (a third), so a long finding is not found by two passing words.</summary>
      public const double MinShare = 1.0 / 3;

      /// <summary>The ids of the findings the narration shows, in the scene's order; empty when none.</summary>
      public static IReadOnlyList<string> Seen(SceneFindingEchoFacts facts)
      {
         var none = new List<string>();
         if (facts?.Left == null || string.IsNullOrWhiteSpace(facts.Narration)) return none;

         var told = new HashSet<string>(LongMemoryRecall.Tokens(facts.Narration));
         var common = new HashSet<string>(LongMemoryRecall.Tokens(facts.SceneWords));
         var seen = new List<string>();
         foreach (SceneFindingText f in facts.Left)
         {
            if (string.IsNullOrEmpty(f?.Id)) continue;

            List<string> own = LongMemoryRecall.Tokens(f.Text).Where(t => !common.Contains(t)).Distinct().ToList();
            if (own.Count == 0) continue;

            int hit = own.Count(told.Contains);
            int need = Math.Max(MinWords, (int) Math.Ceiling(own.Count * MinShare));
            if (hit >= need) seen.Add(f.Id);
         }

         return seen;
      }
   }
}
