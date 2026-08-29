// Code written by Gabriel Mailhot, 28/08/2026.
// Modder request (Heroes Voices, an external TTS/subtitle consumer of CrConversation.LineSpoken/Started/Ended):
// CrSpeechLine.ConversationId is the main NPC's hero StringId, which repeats across every conversation ever
// held with that NPC, so a listener cannot tell two separate exchanges apart, and there is no line-order field
// at all. This sequencer is the pure engine-free half of the fix: it hands out a monotonic LineIndex (0, 1,
// 2, ...) within one conversation, and resets to 0 every time a new session starts. It does NOT generate the
// SessionId itself (that GUID is minted engine-side, in CrConversation.RaiseStarted); this class only tracks
// which session is current and how far into it the count has gone.
//
// WHY the reset matters: a TTS addon keys its audio packets on (SessionId, LineIndex) to group and order
// lines of one exchange, including the multi-participant SPOTLIGHT ensemble conversations introduced in
// v2.3.1 (several voices speaking within the same session). If the counter did not reset per conversation,
// or did not increment at all, two different exchanges could hand out the same (SessionId, LineIndex) pair,
// or a later conversation's first line could collide with an earlier one's tenth: packets would misalign and
// the wrong audio would play against the wrong line.
//
// Single-threaded by contract: CrConversation.RaiseLineSpoken is only ever called from the game's main
// thread (the same thread that renders a chat turn), so this class takes no lock.

namespace CalradiaRemembers.Logic.Extension
{
    /// <summary>
    ///   Sequences line indices within one conversation session. Pure and engine-free: it neither generates
    ///   nor validates the session id, it only remembers the CURRENT one and counts lines within it. Not
    ///   thread-safe by design (see file header); CR calls it only from the main thread.
    /// </summary>
    public sealed class ConversationLineSequencer
    {
        private int _nextIndex;

        /// <summary>The session id passed to the last <see cref="StartSession" /> call, or null before any call.</summary>
        public string SessionId { get; private set; }

        /// <summary>
        ///   Starts a new session: stores <paramref name="sessionId" /> as the current one and resets the line
        ///   counter to 0, so the first <see cref="NextIndex" /> call in the new session returns 0 regardless
        ///   of how far the previous session's count had gone. Returns the stored id for convenience.
        /// </summary>
        public string StartSession(string sessionId)
        {
            SessionId = sessionId;
            _nextIndex = 0;

            return SessionId;
        }

        /// <summary>Returns the current counter value, then increments it (0, 1, 2, ... within the current session).</summary>
        public int NextIndex()
        {
            int current = _nextIndex;
            _nextIndex++;

            return current;
        }
    }
}
