// Code written by Gabriel Mailhot, 18/07/2026.
// One attributed line of conversation history. It lives in Logic rather than in the chat controller because
// nothing about it is engine data: it is four strings and two flags, and every rule that reads it (which
// session each participant receives, what the memory summarizer is handed) is pure and belongs under test.
//
// Immutable on purpose. The transcript is the one shared record every participant's view is rebuilt from, so
// a line that could be edited after the fact would silently rewrite history the model has already been shown.

namespace CalradiaRemembers.Logic
{
    /// <summary>One attributed line of conversation history, as it was recorded.</summary>
    public sealed class TranscriptLine
    {
        public bool IsPlayer { get; init; }

        /// <summary>A [NARRATION] block rather than a spoken line.</summary>
        public bool IsNarration { get; init; } = false;

        public string SpeakerHeroId { get; init; }
        public string SpeakerName { get; init; }
        public string Text { get; init; }
    }
}
