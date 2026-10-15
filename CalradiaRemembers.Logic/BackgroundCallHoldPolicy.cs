// Code written by Gabriel Mailhot, 15/10/2026.
// Whether a BACKGROUND model call (a conversation summary, a memory compaction, a letter, a letter's memory, a
// courier secret's judgement) waits for the open conversation to close before it is sent. fkasad (Nexus,
// 15/10/2026), running small and medium models on a local server: background tasks "slip in between two replies",
// the server swaps its KV cache from the conversation to the background prompt and back, and the next reply is
// slow enough to time out. He asked that they wait until the conversation ends, on the local path only. A cloud
// provider serves each request on its own, so holding there would only delay a letter for nothing.
//
// The hold is capped: the chat's own host documents that its screen can vanish without its close ever running,
// and a flag stuck "open" must not park every letter and summary for the rest of the session.

namespace CalradiaRemembers.Logic
{
    /// <summary>The facts that decide whether a background model call waits for the conversation to close.</summary>
    public sealed class BackgroundCallHoldFacts
    {
        /// <summary>A conversation is open on screen. False: nothing to wait for, the call goes now.</summary>
        public bool ConversationOpen { get; init; }

        /// <summary>
        ///   The call goes to the local (OpenAI-Compatible) server. False for a cloud provider, which serves
        ///   requests side by side, so holding would delay the work for nothing.
        /// </summary>
        public bool GoesToLocalServer { get; init; }

        /// <summary>How long this call has already waited, in real seconds.</summary>
        public double HeldSeconds { get; init; }
    }

    /// <summary>Decides whether a background model call keeps waiting for the conversation to close.</summary>
    public static class BackgroundCallHoldPolicy
    {
        /// <summary>
        ///   The longest a call waits, in real seconds. Past it the call is sent anyway: a conversation that long
        ///   is rare, and a chat flag stuck open must not starve the background work.
        /// </summary>
        public const double MaxHoldSeconds = 600;

        /// <summary>True while the call must keep waiting.</summary>
        public static bool ShouldHold(BackgroundCallHoldFacts facts)
            => facts != null && facts.ConversationOpen && facts.GoesToLocalServer && facts.HeldSeconds < MaxHoldSeconds;
    }
}
