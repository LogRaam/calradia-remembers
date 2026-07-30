// Code written by Gabriel Mailhot, 18/07/2026.
// The shared, attributed record of everything said in one conversation. It used to be a raw List of a class
// nested privately inside ChatViewModel, which put the whole thing out of reach of the tests (the mod is
// net472, the test project is net10) even though not one line of it touches the engine.
//
// It is the conversation's single source of truth, and BOTH of its consumers are load-bearing: every
// participant's ChatSession is rebuilt from it before every LLM call, and the memory summarizer is handed a
// rendering of it to write what the NPC will remember forever. So the transcript owns its own invariants
// (a blank line is never recorded) rather than trusting each caller to remember them.

#region

using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Accumulates the conversation's attributed lines and answers the two questions asked of it: what
    ///   session one participant should receive, and how the whole thing reads as plain text.
    /// </summary>
    public sealed class ConversationTranscript
    {
        /// <summary>Attributed label under which the compressed running summary is injected into a session.</summary>
        public const string SummaryLabel = "Earlier in this conversation";

        private readonly List<TranscriptLine> _lines = new List<TranscriptLine>();

        /// <summary>The lines recorded so far, oldest first.</summary>
        public IReadOnlyList<TranscriptLine> Lines => _lines;

        public int Count => _lines.Count;

        /// <summary>
        ///   A COMPRESSED recap of the older part of THIS conversation, folded once it grows long so every turn
        ///   stays coherent with the whole exchange without replaying an ever-growing raw transcript (Gabriel
        ///   2026-07-30). Empty until the first fold. Distinct from the NPC's persistent memory: this lives and
        ///   dies with the conversation.
        /// </summary>
        public string RunningSummary { get; private set; } = "";

        /// <summary>Lines [0, SummarizedThrough) are folded into <see cref="RunningSummary" /> and no longer replayed raw.</summary>
        public int SummarizedThrough { get; private set; }

        /// <summary>
        ///   Records that <paramref name="throughLineCount" /> lines are now captured by <paramref name="summary" />.
        ///   Monotonic and bounded: the marker never goes backwards and never past the lines actually recorded, so
        ///   a stale or oversized fold can never hide a line that both the summary and the raw replay would miss.
        /// </summary>
        public void ApplySummary(string summary, int throughLineCount)
        {
            if (string.IsNullOrWhiteSpace(summary)) return;

            RunningSummary = summary.Trim();
            int clamped = throughLineCount < SummarizedThrough ? SummarizedThrough : throughLineCount;
            SummarizedThrough = clamped > _lines.Count ? _lines.Count : clamped;
        }

        /// <summary>
        ///   Records one line, unless there is nothing to record. The blank guard lives HERE, at the single
        ///   point of entry, because a blank line is not merely useless: it would take a slot in every prompt
        ///   rebuilt afterwards, and an empty assistant turn teaches the model that saying nothing is a valid
        ///   reply.
        /// </summary>
        public void Record(bool isPlayer, string speakerName, string speakerHeroId, string text, bool isNarration = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _lines.Add(new TranscriptLine(isPlayer, speakerHeroId, speakerName, text, isNarration));
        }

        /// <summary>
        ///   This participant's view of the conversation, rebuilt for the next LLM call.
        ///   See <see cref="SessionHistoryComposer" /> for what each line becomes and why.
        /// </summary>
        public IReadOnlyList<SessionTurn> ComposeSessionFor(string speakerHeroId, int excludeLastN = 0)
        {
            // No fold yet: the whole raw history, exactly as before.
            if (string.IsNullOrEmpty(RunningSummary) || SummarizedThrough <= 0)
                return SessionHistoryComposer.Compose(_lines, speakerHeroId, excludeLastN);

            // Folded: the compressed recap FIRST (as an attributed context line), then only the recent raw lines.
            var turns = new List<SessionTurn>
            {
                new SessionTurn(SessionTurnRole.Witness, SummaryLabel, RunningSummary)
            };

            foreach (SessionTurn turn in SessionHistoryComposer.Compose(_lines, speakerHeroId, excludeLastN, SummarizedThrough))
                turns.Add(turn);

            return turns;
        }

        /// <summary>
        ///   The whole conversation as plain attributed text, for a summarizer.
        ///   <paramref name="playerLabel" /> names the player's lines ("Prisoner" in captive scenes, their
        ///   actual name in ordinary conversations), which is what lets the same rendering serve a captive
        ///   scene and a normal talk without the summarizer having to know which it is reading.
        /// </summary>
        public string ToPlainText(string playerLabel)
        {
            if (_lines.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (TranscriptLine e in _lines)
            {
                // Belt and braces with Record's own guard: this text becomes a PERSISTED memory, so an
                // orphan "Name: " line is worth one cheap check rather than a corrupted recollection.
                if (string.IsNullOrWhiteSpace(e.Text)) continue;

                sb.AppendLine($"{(e.IsPlayer ? playerLabel : e.SpeakerName)}: {e.Text}");
            }

            return sb.ToString();
        }

        /// <summary>
        ///   Lines [from, to) as attributed text, for the running-recap summarizer to fold in. Same attribution
        ///   as <see cref="ToPlainText" /> (the player carries <paramref name="playerLabel" />). A clamped, empty,
        ///   or inverted range yields "".
        /// </summary>
        public string RenderRange(int from, int to, string playerLabel)
        {
            int start = from < 0 ? 0 : from;
            int end = to > _lines.Count ? _lines.Count : to;
            if (end <= start) return "";

            var sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                TranscriptLine e = _lines[i];
                if (string.IsNullOrWhiteSpace(e.Text)) continue;

                sb.AppendLine($"{(e.IsPlayer ? playerLabel : e.SpeakerName)}: {e.Text}");
            }

            return sb.ToString();
        }
    }
}
