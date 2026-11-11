// Code written by Gabriel Mailhot, 10/11/2026.
// What a war council remembers of its own sitting.
//
// Gabriel (10/11/2026): "on fait ... la memoire du conseil". Traced the same day: the council showed the model the last
// 24 lines of the sitting and dropped the rest silently, and the player's own words never reached those lines at all
// (the council turn displays the player's line without recording it, and nothing recorded it after), so a table of four
// forgot everything older than about five turns and never knew what the player had said before this turn. The running
// recap the 1:1 chat folds (ConversationCompressionPolicy, ConversationHistoryCompressor, 30/07/2026) was never called
// from the council. The council now folds too, with dials of its own: a table speaks four or five lines a turn, so the
// 1:1 pacing (fold past 14, keep 4) would spend a recap call almost every turn and keep less than one turn verbatim.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
    /// <summary>When a council sitting folds its older lines into a recap, and which lines it still shows whole. Pure.</summary>
    public static class CouncilSittingPolicy
    {
        /// <summary>TUNE: unfolded lines a sitting tolerates before folding: the window the council always showed.</summary>
        public const int FoldThreshold = 24;

        /// <summary>TUNE: the newest lines a fold leaves whole: about the last two turns of a table of three or four.</summary>
        public const int RecentKept = 10;

        /// <summary>
        ///   TUNE: the most lines shown whole however the fold went: when a recap call fails the sitting still shows what it
        ///   always did, never a growing transcript.
        /// </summary>
        public const int MaxShown = 24;

        /// <summary>Whether to fold now, and which lines, for a sitting of this many lines folded this far.</summary>
        public static ConversationFoldPlan Plan(int totalLines, int summarizedThrough)
            => ConversationCompressionPolicy.Plan(new ConversationCompressionPolicy.TranscriptFold {
                TotalLines = totalLines, SummarizedThrough = summarizedThrough, Threshold = FoldThreshold, RecentKept = RecentKept
            });

        /// <summary>
        ///   The lines the council prompt shows whole, oldest first, as "Name: words" ("You" for the player, "(scene)" for a
        ///   narration): those after the recap, at most <see cref="MaxShown" />. Blank lines are skipped.
        /// </summary>
        public static IReadOnlyList<string> ShownLines(ConversationTranscript transcript)
        {
            var lines = new List<string>();
            if (transcript == null) return lines;

            int from = string.IsNullOrWhiteSpace(transcript.RunningSummary) ? 0 : transcript.SummarizedThrough;
            for (int i = from < 0 ? 0 : from; i < transcript.Count; i++)
            {
                TranscriptLine l = transcript.Lines[i];
                if (l == null || string.IsNullOrWhiteSpace(l.Text)) continue;

                string who = l.IsNarration ? "(scene)" : l.IsPlayer ? "You" : l.SpeakerName ?? "Someone";
                lines.Add($"{who}: {l.Text}");
            }

            return lines.Count > MaxShown ? lines.GetRange(lines.Count - MaxShown, MaxShown) : lines;
        }

        /// <summary>The recap of what the sitting folded away, or null when nothing was folded yet.</summary>
        public static string Recap(ConversationTranscript transcript)
            => transcript == null || string.IsNullOrWhiteSpace(transcript.RunningSummary) || transcript.SummarizedThrough <= 0
                ? null
                : transcript.RunningSummary.Trim();
    }
}
