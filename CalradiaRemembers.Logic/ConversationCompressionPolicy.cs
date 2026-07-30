// Code written by Gabriel Mailhot, 30/07/2026.
// Pure decision for the running-transcript compression (Gabriel 2026-07-30): a long conversation (a council of
// several members, or any exchange that runs on) must pass a COMPRESSED recap of its own history to each turn,
// so every NPC and the player stay coherent without replaying an ever-growing raw transcript that inflates every
// prompt and can overflow a small or reasoning model. This decides WHEN to fold and WHICH lines to fold; the
// LLM call that writes the summary (the engine half) is built separately, and the ConversationTranscript holds
// the result. Keeping it pure means the WHEN/WHICH boundary is unit-tested rather than only seen in a huge scene.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What <see cref="ConversationCompressionPolicy.Plan" /> decides about folding older lines away.</summary>
    public readonly struct ConversationFoldPlan
    {
        /// <summary>True when the older lines should be folded into the running summary now.</summary>
        public bool ShouldFold { get; }

        /// <summary>First line index to fold (inclusive); the point the last summary reached.</summary>
        public int FoldFrom { get; }

        /// <summary>One past the last line to fold (exclusive); the recent lines from here on stay raw.</summary>
        public int FoldTo { get; }

        private ConversationFoldPlan(bool shouldFold, int foldFrom, int foldTo)
        {
            ShouldFold = shouldFold;
            FoldFrom = foldFrom;
            FoldTo = foldTo;
        }

        /// <summary>Nothing to fold this turn: the raw history is still short enough to replay whole.</summary>
        public static ConversationFoldPlan None => new ConversationFoldPlan(false, 0, 0);

        /// <summary>Fold the half-open range [from, to) into the summary.</summary>
        public static ConversationFoldPlan Fold(int from, int to) => new ConversationFoldPlan(true, from, to);
    }

    /// <summary>
    ///   Pure policy: given how many lines a conversation holds and how many are already folded into its running
    ///   summary, decide whether to fold more now and which lines. Folds only once the UNFOLDED lines pass a
    ///   threshold, and always keeps the most recent handful raw so the immediate context stays sharp.
    /// </summary>
    public static class ConversationCompressionPolicy
    {
        /// <summary>Fold once this many lines have piled up UNFOLDED (raw) since the last summary. Tunable.</summary>
        public const int UnfoldedLineThreshold = 14;

        /// <summary>The most recent lines always replayed raw (never folded), so recent context stays verbatim. Tunable.</summary>
        public const int RecentLinesKeptVerbatim = 4;

        /// <summary>Decides the fold using the tuned defaults.</summary>
        public static ConversationFoldPlan Plan(int totalLines, int summarizedThrough)
            => Plan(totalLines, summarizedThrough, UnfoldedLineThreshold, RecentLinesKeptVerbatim);

        /// <summary>
        ///   Decides the fold. Folds the range from where the last summary reached up to (but not including) the
        ///   last <paramref name="recentKept" /> lines, but only once the unfolded count exceeds
        ///   <paramref name="threshold" /> and there is actually a non-empty older range to fold.
        /// </summary>
        public static ConversationFoldPlan Plan(int totalLines, int summarizedThrough, int threshold, int recentKept)
        {
            int from = Math.Max(0, summarizedThrough);
            int unfolded = totalLines - from;
            int to = totalLines - Math.Max(0, recentKept);

            if (unfolded <= Math.Max(1, threshold) || to <= from) return ConversationFoldPlan.None;

            return ConversationFoldPlan.Fold(from, to);
        }
    }
}
