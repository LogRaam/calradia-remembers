// Code written by Gabriel Mailhot, 25/10/2026.
// When the action interpreter's call brings back nothing usable (the call failed or timed out, the reply was empty, or it
// could not be parsed), the reply's deeds used to be lost with a log line only: the prose had shown, the scene went on,
// and nothing recorded what the character had done. Gabriel (25/10/2026), asked what should happen then: "reessaie une
// fois l'interpreteur en ajoutant un log qui le specifie." A reading that worked but found no deed is an answer, not a
// failure, and is never retried.
//
// FailureAdvice: avalanche24 (Nexus, 03/11/2026) sent a log where the interpreter's online provider answered
// "HTTP 503: Service is too busy" twice, and our line told them their local model's context was too small. The
// advice now follows what failed: a busy provider, a call that failed otherwise, or a reply the model got wrong.

#region

using System.Text.RegularExpressions;

#endregion

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

        private static readonly Regex ProviderSide = new Regex(@"\bHTTP\s*(5\d\d|429)\b|too busy|overloaded|rate.?limit",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        ///   The advice the log gives after the last failed attempt: a provider that was busy or rate-limited is named as
        ///   such; another failed call points at the provider, key and model; only an empty or unreadable reply points at
        ///   a model too small for the interpreter.
        /// </summary>
        public static string FailureAdvice(InterpreterAttemptOutcome lastOutcome, string error)
        {
            if (lastOutcome == InterpreterAttemptOutcome.CallFailed && error != null && ProviderSide.IsMatch(error))
                return "The interpreter's provider was busy or limiting requests: this is on their side, not the model's. "
                     + "Wait a moment, or pick another provider or model for the Actions Interpreter.";

            if (lastOutcome == InterpreterAttemptOutcome.CallFailed)
                return "The call itself failed: check the Actions Interpreter's provider, API key and model name.";

            return "If this repeats on a local model, its context is too small for the interpreter: turn on "
                 + "'Compact prompt for small models', or switch Chat Composition to Standard so the scene model "
                 + "emits its own tags in one call.";
        }
    }
}
