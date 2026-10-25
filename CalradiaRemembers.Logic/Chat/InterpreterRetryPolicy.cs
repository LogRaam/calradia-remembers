// Code written by Gabriel Mailhot, 25/10/2026.
// When the action interpreter's call brings back nothing usable (the call failed or timed out, the reply was empty, or it
// could not be parsed), the reply's deeds used to be lost with a log line only: the prose had shown, the scene went on,
// and nothing recorded what the character had done. Gabriel (25/10/2026), asked what should happen then: "reessaie une
// fois l'interpreteur en ajoutant un log qui le specifie." A reading that worked but found no deed is an answer, not a
// failure, and is never retried.

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>What one interpreter call brought back.</summary>
    public enum InterpreterAttemptOutcome
    {
        /// <summary>A reply that parsed: its tags, possibly none, are the reading.</summary>
        Read,

        /// <summary>The call failed or timed out.</summary>
        CallFailed,

        /// <summary>The call answered with nothing.</summary>
        Empty,

        /// <summary>The reply could not be parsed.</summary>
        Unparsable
    }

    /// <summary>Whether the interpreter is asked again after an attempt. Pure.</summary>
    public static class InterpreterRetryPolicy
    {
        /// <summary>The first call and one more (Gabriel, 25/10/2026: "reessaie une fois").</summary>
        public const int MaxAttempts = 2;

        /// <summary>True when an attempt brought back nothing usable and the interpreter has not yet been asked twice.</summary>
        public static bool ShouldRetry(InterpreterAttemptOutcome outcome, int attemptsMade)
            => outcome != InterpreterAttemptOutcome.Read && attemptsMade < MaxAttempts;
    }
}
