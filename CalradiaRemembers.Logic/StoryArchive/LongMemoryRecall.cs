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

      /// <summary>The threads this turn is on before any search: the people and places the player's line names, and the
      ///   threads of the conversation under way.</summary>
      public IReadOnlyCollection<string> QueryThreads { get; init; }
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

      /// <summary>The threads of this turn its conversation is on ("place:town_V1"...).</summary>
      public IReadOnlyList<string> ThreadKeys { get; init; }

      /// <summary>The newest of several passages on one of those threads: how the matter stands now.</summary>
      public bool IsLatestOnThread { get; set; }
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

      /// <summary>
      ///   The passages to recall, oldest first; empty when nothing bears on this turn. Two roads lead to a passage: the
      ///   words (BM25 over the lines the character heard) and the THREADS (Gabriel, 17/11/2026: a mission spoken of in
      ///   several conversations must link them). The best word match seeds the threads the turn is on, with those the
      ///   player's own line names; every earlier conversation on one of them then gives its best passage, newest first,
      ///   before the remaining word matches, all within the budget. On each thread the newest passage is marked: it says
      ///   how the matter stands now. When neither words nor names find anything, the threads of the newest conversation on
      ///   one are taken: the matter last spoken of.
      /// </summary>
      public static IReadOnlyList<LongMemoryPassage> Find(LongMemoryRecallFacts facts)
      {
         var none = new List<LongMemoryPassage>();
         if (facts?.Records == null || string.IsNullOrEmpty(facts.CharacterId) || facts.BudgetChars <= 0) return none;

         HashSet<string> query = new HashSet<string>(Tokens(facts.Query));
         HashSet<string> queryThreads = new HashSet<string>((facts.QueryThreads ?? new string[0]).Where(k => !string.IsNullOrEmpty(k)));

         // The lines this character may recall, by conversation, in the order said.
         Dictionary<string, List<StoryArchiveRecord>> byEpisode = facts.Records
            .Where(r => r != null && r.Kind == StoryArchiveKind.Line && !string.IsNullOrWhiteSpace(r.Text)
                        && !string.IsNullOrEmpty(r.EpisodeId) && r.EpisodeId != facts.CurrentEpisodeId
                        && (r.SpeakerId == facts.CharacterId || r.Audience?.Contains(facts.CharacterId) == true)
                        && facts.AlreadyInContext?.Contains(r.Text) != true)
            .GroupBy(r => r.EpisodeId)
            .ToDictionary(g => g.Key, g => g.ToList());
         if (byEpisode.Count == 0) return none;

         // The threads each of those conversations is on: its lines' own, and those learned after (Thread records).
         Dictionary<string, HashSet<string>> episodeThreads = byEpisode.ToDictionary(e => e.Key, e => new HashSet<string>(e.Value.SelectMany(l => l.Threads ?? new List<string>())));
         foreach (StoryArchiveRecord r in facts.Records)
            if (r?.Kind == StoryArchiveKind.Thread && r.Threads != null && r.EpisodeId != null && episodeThreads.TryGetValue(r.EpisodeId, out HashSet<string> set))
               set.UnionWith(r.Threads);

         var docs = new List<(string Episode, int Index, List<string> Tokens)>();
         foreach (KeyValuePair<string, List<StoryArchiveRecord>> e in byEpisode)
            for (var i = 0; i < e.Value.Count; i++)
               docs.Add((e.Key, i, Tokens(e.Value[i].Text).ToList()));

         double avgLen = docs.Average(d => (double) Math.Max(1, d.Tokens.Count));
         Dictionary<string, int> docFreq = query.ToDictionary(t => t, t => docs.Count(d => d.Tokens.Contains(t)));
         var scores = new Dictionary<(string, int), double>();
         foreach ((string episode, int index, List<string> tokens) in docs)
            scores[(episode, index)] = query.Count == 0 ? 0 : Bm25(tokens, query, docFreq, docs.Count, avgLen);
         List<(string Episode, int Index)> scored = scores.Where(s => s.Value > 0).OrderByDescending(s => s.Value).Select(s => s.Key).ToList();

         // The threads this turn is on: those the player's line names, and those of the best word match: its line's own, or,
         // when that line names nothing, its conversation's (A/B of 17/11/2026: "By when do you need it done?" best matched
         // "See it done before the harvest", a line naming no place, so the talk that moved the deadline never came).
         var onThreads = new HashSet<string>(queryThreads);
         if (scored.Count > 0)
         {
            List<string> bestLine = byEpisode[scored[0].Episode][scored[0].Index].Threads;
            onThreads.UnionWith(bestLine != null && bestLine.Count > 0 ? (IEnumerable<string>) bestLine : episodeThreads[scored[0].Episode]);
         }

         // Nothing in the player's words and no thread named: the matter last spoken of with this character (Gabriel,
         // 17/11/2026: "oui, rappelle le fil le plus recent", after the A/B left "Remind me, what exactly is the task now?"
         // with nothing recalled, a line sharing no word and no name with the mission). Only a conversation on a thread: with
         // none, nothing comes, as before.
         if (scored.Count == 0 && onThreads.Count == 0)
         {
            string last = byEpisode.Keys.Where(e => episodeThreads[e].Count > 0).OrderByDescending(e => byEpisode[e][0].Time).FirstOrDefault();
            if (last != null) onThreads.UnionWith(episodeThreads[last]);
         }

         if (scored.Count == 0 && onThreads.Count == 0) return none;

         var candidates = new List<(string Episode, int Index)>();
         if (scored.Count > 0) candidates.Add(scored[0]);
         foreach (string episode in byEpisode.Keys.Where(e => onThreads.Count > 0 && episodeThreads[e].Overlaps(onThreads))
                                                 .OrderByDescending(e => byEpisode[e][0].Time))
         {
            List<StoryArchiveRecord> lines = byEpisode[episode];
            List<int> onIt = Enumerable.Range(0, lines.Count).Where(i => lines[i].Threads?.Any(onThreads.Contains) == true).ToList();
            List<int> pool = onIt.Count > 0 ? onIt : Enumerable.Range(0, lines.Count).ToList();
            int best = pool.OrderByDescending(i => scores[(episode, i)]).ThenByDescending(i => i).First();
            candidates.Add((episode, best));
         }

         candidates.AddRange(scored.Skip(1));

         // The newest conversation on the turn's threads says how the matter stands now: it comes WHOLE when the budget allows
         // (A/B of 17/11/2026: the window around "the bandits near Varcheg have taken my nephew" stopped two lines short of the
         // new reward, and the lord invented one).
         string newest = onThreads.Count == 0 ? null : byEpisode.Keys.Where(e => episodeThreads[e].Overlaps(onThreads))
                                                                .OrderByDescending(e => byEpisode[e][0].Time).FirstOrDefault();

         // Windows around the candidates, merged when they touch, until the budget is spent.
         var windows = new Dictionary<string, List<(int From, int To)>>();
         var spent = 0;
         if (newest != null)
         {
            List<StoryArchiveRecord> lines = byEpisode[newest];
            int whole = Chars(lines, (0, lines.Count - 1));
            // At most half the budget, so a long talk never crowds out the rest of the story.
            if (whole <= facts.BudgetChars / 2)
            {
               windows[newest] = new List<(int From, int To)> {(0, lines.Count - 1)};
               spent += whole;
            }
         }

         foreach ((string episode, int index) in candidates)
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

         List<LongMemoryPassage> passages = windows
            .SelectMany(e => e.Value.Select(w => Passage(facts.CharacterId, byEpisode[e.Key], w,
               episodeThreads[e.Key].Where(onThreads.Contains).OrderBy(k => k, StringComparer.Ordinal).ToList())))
            .OrderBy(p => p.Time)
            .ToList();

         // On each thread more than one passage carries, the newest says how the matter stands now.
         foreach (string key in onThreads)
         {
            List<LongMemoryPassage> on = passages.Where(p => p.ThreadKeys.Contains(key)).ToList();
            if (on.Count > 1) on[on.Count - 1].IsLatestOnThread = true;
         }

         return passages;
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

      /// <summary>BM25 of one passage against the query's words (shared with EventDetailRecall, so both rank alike).</summary>
      internal static double Bm25(List<string> doc, HashSet<string> query, Dictionary<string, int> docFreq, int n, double avgLen)
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

      private static LongMemoryPassage Passage(string characterId, List<StoryArchiveRecord> lines, (int From, int To) w, IReadOnlyList<string> threadKeys)
      {
         List<StoryArchiveRecord> taken = lines.Skip(w.From).Take(w.To - w.From + 1).ToList();

         return new LongMemoryPassage {
            EpisodeId = taken[0].EpisodeId, Time = taken[0].Time, PlaceName = taken.Select(l => l.PlaceName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
            Lines = taken,
            AlsoPresentIds = taken.SelectMany(l => l.Audience ?? new List<string>()).Where(id => id != characterId).Distinct().ToList(),
            ThreadKeys = threadKeys
         };
      }
   }
}
