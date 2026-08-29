// Code written by Gabriel Mailhot, 29/08/2026.
// Pure: a modder report that asking a specific lord about a specific war/lord got a hallucinated answer,
// because the real fact ranked too low on ambient AWARENESS to make the small top-N cut. This scores how
// RELEVANT an already-KNOWN world fact is to what the player just said, so a low-awareness-but-known fact
// can still surface when it actually answers the question. Deliberately downstream of the knowledge gate:
// the caller must filter to facts the NPC would plausibly know FIRST, and only then hand candidates here.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The result of scoring one world-fact candidate against the player's message.</summary>
    public readonly struct WorldFactRelevance
    {
        public WorldFactRelevance(int score, bool isRelevant, bool entityMatch, int keywordOverlapCount)
        {
            Score = score;
            IsRelevant = isRelevant;
            EntityMatch = entityMatch;
            KeywordOverlapCount = keywordOverlapCount;
        }

        /// <summary>True when the event names a hero/faction the player also named: the strongest signal.</summary>
        public bool EntityMatch { get; }

        /// <summary>True when <see cref="Score" /> clears <see cref="WorldFactRelevancePolicy.RelevanceThreshold" />.</summary>
        public bool IsRelevant { get; }

        /// <summary>How many meaningful keyword tokens the event summary shares with the message.</summary>
        public int KeywordOverlapCount { get; }

        /// <summary>The raw relevance score (see <see cref="WorldFactRelevancePolicy" /> weights).</summary>
        public int Score { get; }
    }

    /// <summary>
    ///   Pure, engine-free scoring of how much a world fact bears on what the player just said. Feeds a
    ///   SECOND, message-relevant fact block alongside the existing ambient "what you've heard" rumors:
    ///   awareness alone picks a small top-N by magnitude/faction/distance/recency, which starves a fact
    ///   that IS known to the NPC but never ranks high enough to be injected, so the model hallucinates an
    ///   answer instead. Order is mandatory downstream: a KNOWLEDGE filter runs first (never surface a fact
    ///   the NPC would not plausibly know), THEN this relevance scoring picks among the survivors. No LLM
    ///   call, fully deterministic, unit-tested so the weights and threshold are pinned.
    /// </summary>
    public static class WorldFactRelevancePolicy
    {
        /// <summary>
        ///   Flat score for an EXACT entity-id match (the event references a hero/faction the player named):
        ///   the strongest signal there is, and alone enough to clear <see cref="RelevanceThreshold" /> many
        ///   times over.
        /// </summary>
        public const int EntityMatchScore = 100;

        /// <summary>A keyword at/above this length is distinctive enough to score the higher weight.</summary>
        public const int LongKeywordLength = 6;

        /// <summary>Weight added per overlapping LONG (6+ char) keyword token, a rarer/more specific word.</summary>
        public const int LongKeywordWeight = 20;

        /// <summary>Keyword tokens shorter than this are too generic to carry meaning and are dropped.</summary>
        public const int MinKeywordLength = 3;

        /// <summary>
        ///   Score at/above which a candidate counts as relevant. Set so a SINGLE short keyword overlap
        ///   (<see cref="ShortKeywordWeight" /> = 10) never crosses it alone, a lone generic word is not
        ///   enough to trust, but two short overlaps, or one long/distinctive one, do.
        /// </summary>
        public const int RelevanceThreshold = 20;

        /// <summary>Weight added per overlapping SHORT (3..5 char) keyword token.</summary>
        public const int ShortKeywordWeight = 10;

        // Small, deliberately conservative stop-word list: only common connector/filler words that would
        // otherwise spuriously "overlap" between almost any event summary and almost any message. Not the
        // full multi-language list ContextualNameResolver uses for hero-name matching (a different job);
        // this only needs to keep keyword overlap meaningful.
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "but", "for", "you", "your", "him", "her", "his", "she", "they", "their",
            "who", "what", "how", "why", "when", "where", "this", "that", "with", "from", "have",
            "been", "not", "are", "was", "were", "will", "can", "did", "has", "had", "out", "about",
            "les", "des", "que", "qui", "une", "est", "pas", "sur", "par", "pour", "dans", "avec",
            "son", "ses", "lui", "elle", "ils", "eux", "leur", "tout", "bien", "mais", "donc"
        };

        /// <summary>
        ///   Splits free text into meaningful, lowercase keyword tokens: words of at least
        ///   <see cref="MinKeywordLength" /> letters, stop words dropped. Used to tokenize an event's
        ///   summary, and available to callers who need the same extraction over the player's message
        ///   (mirrors <c>ContextualNameResolver.ExtractTokens</c>'s filtering, kept independent here so
        ///   this project stays engine-free).
        /// </summary>
        public static HashSet<string> Tokenize(string text)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return tokens;

            foreach (string word in Regex.Split(text, @"[^\p{L}]+"))
            {
                if (word.Length < MinKeywordLength) continue;
                if (StopWords.Contains(word)) continue;

                tokens.Add(word.ToLowerInvariant());
            }

            return tokens;
        }

        /// <summary>
        ///   Scores one candidate world fact against the player's message signals. The caller must already
        ///   have filtered candidates by KNOWLEDGE (the NPC would plausibly know this fact); this only ranks
        ///   relevance among facts already cleared for that, it never widens who is allowed to know what.
        /// </summary>
        /// <param name="eventEntityIds">The event's own hero/faction/actor StringIds.</param>
        /// <param name="eventSummary">The fact's ready-to-inject summary line.</param>
        /// <param name="messageEntityIds">Hero/faction StringIds the player named in their message.</param>
        /// <param name="messageKeywordTokens">
        ///   The message's own keyword tokens. Re-filtered here (length + stop words) so a caller passing
        ///   raw, unfiltered tokens cannot spuriously inflate the overlap with short or filler words.
        /// </param>
        public static WorldFactRelevance Evaluate(
            IEnumerable<string> eventEntityIds,
            string eventSummary,
            IEnumerable<string> messageEntityIds,
            IEnumerable<string> messageKeywordTokens)
        {
            bool entityMatch = HasEntityMatch(eventEntityIds, messageEntityIds);

            HashSet<string> summaryTokens = Tokenize(eventSummary);
            HashSet<string> messageTokens = FilterMeaningful(messageKeywordTokens);

            int overlapCount = 0;
            int keywordScore = 0;
            foreach (string token in messageTokens)
            {
                if (!summaryTokens.Contains(token)) continue;

                overlapCount++;
                keywordScore += token.Length >= LongKeywordLength ? LongKeywordWeight : ShortKeywordWeight;
            }

            int score = (entityMatch ? EntityMatchScore : 0) + keywordScore;

            return new WorldFactRelevance(score, score >= RelevanceThreshold, entityMatch, overlapCount);
        }

        private static HashSet<string> FilterMeaningful(IEnumerable<string> tokens)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tokens == null) return result;

            foreach (string token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                string t = token.Trim().ToLowerInvariant();
                if (t.Length < MinKeywordLength) continue;
                if (StopWords.Contains(t)) continue;

                result.Add(t);
            }

            return result;
        }

        private static bool HasEntityMatch(IEnumerable<string> eventEntityIds, IEnumerable<string> messageEntityIds)
        {
            if (eventEntityIds == null || messageEntityIds == null) return false;

            var messageSet = new HashSet<string>(
                messageEntityIds.Where(id => !string.IsNullOrEmpty(id)),
                StringComparer.OrdinalIgnoreCase);

            return messageSet.Count > 0 && eventEntityIds.Any(id => !string.IsNullOrEmpty(id) && messageSet.Contains(id));
        }
    }
}
