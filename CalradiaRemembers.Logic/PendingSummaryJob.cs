// Code written by Gabriel Mailhot, 24/07/2026.
// Durable async-summary queue, increment B (pure logic only). When a conversation closes, the mod records a
// base memory event ("Spoke with X.") then fires an async LLM call to replace it with a rich summary. If the
// game closes or crashes before that call returns, the plan is lost and the NPC remembers nothing but the bare
// placeholder forever. A PendingSummaryJob is that plan, captured as plain data so it survives a save/quit and
// can be replayed on the next load: everything the summarizer needs (the transcript, player name, reply
// language) plus the value-match key (profile, day, event type, base summary text) that lets a worker re-find
// the SAME base event after a reload, when the original in-memory event reference is long gone.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   One pending "write the rich summary and replace the base event" job, captured as plain data so it can
    ///   be persisted (a later increment wires it into SyncData) and replayed after a game reload. The anchor
    ///   fields (<see cref="AnchorDay" />, <see cref="AnchorType" />, <see cref="AnchorSummary" />) are a
    ///   VALUE-MATCH key: the worker re-finds the base event to replace by matching these, since the original
    ///   in-memory event reference does not survive a save/load round-trip.
    /// </summary>
    public sealed class PendingSummaryJob
    {
        /// <summary>The NPC profile whose memory this job enriches.</summary>
        public string ProfileId { get; set; }

        /// <summary>Part of the anchor key: the in-game day the base event was recorded on.</summary>
        public int AnchorDay { get; set; }

        /// <summary>Part of the anchor key: the notable-event-type name of the base event.</summary>
        public string AnchorType { get; set; }

        /// <summary>Part of the anchor key: the base event's placeholder text ("Spoke with X."), replaced once the rich summary lands.</summary>
        public string AnchorSummary { get; set; }

        /// <summary>The conversation transcript the summarizer needs as its input.</summary>
        public string Transcript { get; set; }

        /// <summary>The player's display name, as the summarizer prompt expects it.</summary>
        public string PlayerName { get; set; }

        /// <summary>The reply language requested for the summary, or null/empty for the default.</summary>
        public string ReplyLanguage { get; set; }

        /// <summary>The in-game day this job was enqueued, for diagnostics and staleness checks.</summary>
        public int EnqueuedDay { get; set; }

        /// <summary>How many times processing has been attempted so far, for retry/drop (see <see cref="ConversationSummaryQueue" />).</summary>
        public int Attempts { get; set; }
    }
}
