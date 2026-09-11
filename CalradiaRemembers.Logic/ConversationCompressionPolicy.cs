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

        /// <summary>
        ///   Where a transcript stands, and the two dials that decide when it folds.
        ///   <para>
        ///     Named rather than positional (2026-09-10). Plan took FOUR INTS in a row, all line counts, and
        ///     no two of them meaning the same kind of thing: a total, a watermark, a threshold and a keep-back.
        ///     Swap the last two and a conversation folds after four lines instead of fourteen, keeping
        ///     fourteen verbatim, which is not a crash but a prompt three times the size anybody budgeted for.
        ///   </para>
        ///   <para>
        ///     The two dials carry the tuned defaults here, which retires the two-argument convenience overload
        ///     that existed only to supply them. One method now, and a caller that wants the shipped pacing
        ///     simply does not mention it.
        ///   </para>
        /// </summary>
        public sealed class TranscriptFold
        {
            /// <summary>How many lines the transcript holds in all.</summary>
            public int TotalLines { get; set; }

            /// <summary>How far a previous summary already reached. Lines before this are folded away already.</summary>
            public int SummarizedThrough { get; set; }

            /// <summary>Unfolded lines tolerated before a fold is worth doing. Defaults to the shipped pacing.</summary>
            public int Threshold { get; set; } = UnfoldedLineThreshold;

            /// <summary>How many of the newest lines stay verbatim, never folded. Defaults to the shipped pacing.</summary>
            public int RecentKept { get; set; } = RecentLinesKeptVerbatim;
        }

        /// <summary>
        ///   Decides the fold. Folds the range from where the last summary reached up to (but not including) the
        ///   last <see cref="TranscriptFold.RecentKept" /> lines, but only once the unfolded count exceeds
        ///   <see cref="TranscriptFold.Threshold" /> and there is actually a non-empty older range to fold.
        /// </summary>
        public static ConversationFoldPlan Plan(TranscriptFold transcript)
        {
            if (transcript == null) return ConversationFoldPlan.None;

            int totalLines = transcript.TotalLines;
            int summarizedThrough = transcript.SummarizedThrough;
            int threshold = transcript.Threshold;
            int recentKept = transcript.RecentKept;

            int from = Math.Max(0, summarizedThrough);
            int unfolded = totalLines - from;
            int to = totalLines - Math.Max(0, recentKept);

            if (unfolded <= Math.Max(1, threshold) || to <= from) return ConversationFoldPlan.None;

            return ConversationFoldPlan.Fold(from, to);
        }
    }
}
