// Code written by Gabriel Mailhot, 17/11/2026.
// Goeth (Discord, 17/11/2026): "Before each reply, CR would search those lines for what matters now: by words (BM25) ... A few
// short blocks around the best matches would go into the prompt, about 5 lines each, with the match in the middle and never
// crossing into another conversation ... The current conversation would be left out, and a character-budget slider would
// keep the cost in check." Gabriel (17/11/2026): off by default; captive scenes may be recalled; a character recalls only
// words said where they were PRESENT (the audience written on each line of the story archive). Overheard-unawares and an
// envoy's report to his lord are later steps; until then a lord recalls nothing of an audience his envoy held.
//
// If this is wrong: a character quotes words said where they never were, quotes the conversation the model is already
// reading, splices two different nights into one passage, or swells the prompt past the player's budget.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.StoryArchive
{
   /// <summary>What the recall reads for one character on one turn.</summary>
   public sealed class LongMemoryRecallFacts
   {
      /// <summary>The story archive branch the loaded save belongs to, oldest first.</summary>
      public IReadOnlyList<StoryArchiveRecord> Records { get; init; }

      /// <summary>The character who recalls (hero StringId, or a nemesis profile id).</summary>
      public string CharacterId { get; init; }

      /// <summary>What is being said now: the player's line, and what else the turn is about.</summary>
      public string Query { get; init; }

      /// <summary>The conversation under way: never recalled, the model reads it already.</summary>
      public string CurrentEpisodeId { get; init; }

      /// <summary>Lines already in the conversation's own history (a resumed talk): never recalled twice.</summary>
      public ISet<string> AlreadyInContext { get; init; }

      /// <summary>The most characters of recalled words.</summary>
      public int BudgetChars { get; init; } = LongMemoryRecall.DefaultBudgetChars;
   }

   /// <summary>One recalled passage: consecutive lines of one earlier conversation.</summary>
   public sealed class LongMemoryPassage
   {
      /// <summary>The conversation it comes from.</summary>
      public string EpisodeId { get; init; }

      /// <summary>When it was said (campaign days).</summary>
      public double Time { get; init; }

      /// <summary>Where it was said, as told.</summary>
      public string PlaceName { get; init; }

      /// <summary>The lines, in order.</summary>
      public IReadOnlyList<StoryArchiveRecord> Lines { get; init; }

      /// <summary>The others who heard these lines, besides the recalling character (hero ids).</summary>
      public IReadOnlyList<string> AlsoPresentIds { get; init; }
   }

   /// <summary>Finds the few passages of earlier conversations that bear on this turn. Pure.</summary>
   public static class LongMemoryRecall
   {
      /// <summary>The default budget: about a thousand tokens.</summary>
      public const int DefaultBudgetChars = 4000;

      /// <summary>Lines kept on each side of a match: five lines a passage, the match in the middle (Goeth's measure).</summary>
      public const int Radius = 2;

      /// <summary>BM25 term saturation: the textbook 1.2 (a word said twice counts a little more than once, not twice).</summary>
      private const double K1 = 1.2;

      /// <summary>BM25 length normalisation: the textbook 0.75 (a long line does not win by length alone).</summary>
      private const double B = 0.75;

      private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
         "the", "and", "you", "your", "for", "are", "but", "not", "with", "this", "that", "have", "was", "his", "her", "him", "she",
         "they", "them", "what", "who", "will", "would", "could", "should", "there", "their", "then", "than", "from", "into", "about",
         "been", "were", "our", "out", "all", "any", "can", "did", "does", "has", "had", "its", "let", "may", "now", "one", "own",
         "too", "very", "just", "here", "when", "where", "why", "how", "yes", "well", "said", "say", "says", "tell", "told", "know"
      };

      /// <summary>The passages to recall, oldest first; empty when nothing bears on this turn.</summary>
      public static IReadOnlyList<LongMemoryPassage> Find(LongMemoryRecallFacts facts)
      {
         var none = new List<LongMemoryPassage>();
         if (facts?.Records == null || string.IsNullOrEmpty(facts.CharacterId) || facts.BudgetChars <= 0) return none;

         HashSet<string> query = new HashSet<string>(Tokens(facts.Query));
         if (query.Count == 0) return none;

         // The lines this character may recall, by conversation, in the order said.
         Dictionary<string, List<StoryArchiveRecord>> byEpisode = facts.Records
            .Where(r => r != null && r.Kind == StoryArchiveKind.Line && !string.IsNullOrWhiteSpace(r.Text)
                        && !string.IsNullOrEmpty(r.EpisodeId) && r.EpisodeId != facts.CurrentEpisodeId
                        && (r.SpeakerId == facts.CharacterId || r.Audience?.Contains(facts.CharacterId) == true)
                        && facts.AlreadyInContext?.Contains(r.Text) != true)
            .GroupBy(r => r.EpisodeId)
            .ToDictionary(g => g.Key, g => g.ToList());
         if (byEpisode.Count == 0) return none;

         var docs = new List<(string Episode, int Index, List<string> Tokens)>();
         foreach (KeyValuePair<string, List<StoryArchiveRecord>> e in byEpisode)
            for (var i = 0; i < e.Value.Count; i++)
               docs.Add((e.Key, i, Tokens(e.Value[i].Text).ToList()));

         double avgLen = docs.Average(d => (double) Math.Max(1, d.Tokens.Count));
         Dictionary<string, int> docFreq = query.ToDictionary(t => t, t => docs.Count(d => d.Tokens.Contains(t)));
         var scored = docs.Select(d => (d.Episode, d.Index, Score: Bm25(d.Tokens, query, docFreq, docs.Count, avgLen)))
                          .Where(s => s.Score > 0)
                          .OrderByDescending(s => s.Score)
                          .ToList();

         // Windows around the best matches, merged when they touch, until the budget is spent.
         var windows = new Dictionary<string, List<(int From, int To)>>();
         var spent = 0;
         foreach ((string episode, int index, double _) in scored)
         {
            List<StoryArchiveRecord> lines = byEpisode[episode];
            int from = Math.Max(0, index - Radius), to = Math.Min(lines.Count - 1, index + Radius);
            if (!windows.TryGetValue(episode, out List<(int From, int To)> list)) windows[episode] = list = new List<(int, int)>();
            if (list.Any(w => index >= w.From && index <= w.To)) continue;

            List<(int From, int To)> merged = Merge(list, (from, to));
            int cost = merged.Sum(w => Chars(lines, w)) - list.Sum(w => Chars(lines, w));
            if (spent + cost > facts.BudgetChars) continue;

            spent += cost;
            windows[episode] = merged;
         }

         return windows.SelectMany(e => e.Value.Select(w => Passage(facts.CharacterId, byEpisode[e.Key], w)))
                       .OrderBy(p => p.Time)
                       .ToList();
      }

      /// <summary>
      ///   A word cut to its stem, so "promised" finds "promise" (the A/B of 17/11/2026: "about what you promised me that
      ///   night" recalled the goat story instead of the promise, because no line said "promised"). A light English cut:
      ///   plural and verb endings, then a final e. Over-cutting only merges near words, which BM25 tolerates.
      /// </summary>
      public static string Stem(string word)
      {
         string w = word ?? "";
         if (w.Length > 4 && w.EndsWith("ies", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 3) + "y";
         else if (w.Length > 5 && w.EndsWith("ing", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 3);
         else if (w.Length > 4 && w.EndsWith("ed", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 2);
         else if (w.Length > 4 && w.EndsWith("es", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 2);
         else if (w.Length > 3 && w.EndsWith("s", StringComparison.Ordinal) && !w.EndsWith("ss", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 1);
         if (w.Length > 4 && w.EndsWith("e", StringComparison.Ordinal)) w = w.Substring(0, w.Length - 1);

         return w;
      }

      /// <summary>The words of a text that can carry a match: lower case, three letters or more, not a stop word, stemmed.</summary>
      public static IEnumerable<string> Tokens(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) yield break;

         var word = new System.Text.StringBuilder();
         foreach (char c in text + " ")
         {
            if (char.IsLetterOrDigit(c))
            {
               word.Append(char.ToLowerInvariant(c));

               continue;
            }

            if (word.Length >= 3 && !StopWords.Contains(word.ToString())) yield return Stem(word.ToString());
            word.Clear();
         }
      }

      private static double Bm25(List<string> doc, HashSet<string> query, Dictionary<string, int> docFreq, int n, double avgLen)
      {
         double score = 0;
         int len = Math.Max(1, doc.Count);
         foreach (string term in query)
         {
            int tf = doc.Count(t => t == term);
            if (tf == 0) continue;

            double idf = Math.Log(1 + (n - docFreq[term] + 0.5) / (docFreq[term] + 0.5));
            score += idf * tf * (K1 + 1) / (tf + K1 * (1 - B + B * len / avgLen));
         }

         return score;
      }

      private static List<(int From, int To)> Merge(List<(int From, int To)> list, (int From, int To) add)
      {
         var all = new List<(int From, int To)>(list) {add};
         all.Sort((a, b) => a.From.CompareTo(b.From));
         var merged = new List<(int From, int To)>();
         foreach ((int From, int To) w in all)
            if (merged.Count > 0 && w.From <= merged[merged.Count - 1].To + 1)
               merged[merged.Count - 1] = (merged[merged.Count - 1].From, Math.Max(merged[merged.Count - 1].To, w.To));
            else
               merged.Add(w);

         return merged;
      }

      private static int Chars(List<StoryArchiveRecord> lines, (int From, int To) w)
      {
         var sum = 0;
         for (int i = w.From; i <= w.To; i++) sum += (lines[i].Text?.Length ?? 0) + 12;

         return sum;
      }

      private static LongMemoryPassage Passage(string characterId, List<StoryArchiveRecord> lines, (int From, int To) w)
      {
         List<StoryArchiveRecord> taken = lines.Skip(w.From).Take(w.To - w.From + 1).ToList();

         return new LongMemoryPassage {
            EpisodeId = taken[0].EpisodeId, Time = taken[0].Time, PlaceName = taken.Select(l => l.PlaceName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
            Lines = taken,
            AlsoPresentIds = taken.SelectMany(l => l.Audience ?? new List<string>()).Where(id => id != characterId).Distinct().ToList()
         };
      }
   }
}
