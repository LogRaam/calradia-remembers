// Code written by Gabriel Mailhot, 29/08/2026.
// A [WITNESS_REACTION] can be a pure stage-direction/gesture ("*smirk deepens, remains silent, fingers
// drumming*") with no spoken words at all. Player report: the main NPC's own single LLM call authors both
// its [DIALOGUE] and a bystander's gesture-only reaction, and the Sprint 15C auto-exchange then fires an
// automatic counter-reply that treats the invented silence as being blown off ("silent treatment"), so the
// same model escalates against a gesture it wrote itself. This is the pure classifier the 15C escalation
// gate consults: a gesture-only reaction still renders normally, it just must never DRIVE the auto-reply.

#region

using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Witnesses
{
    /// <summary>
    ///   Classifies a witness reaction's text as containing actual SPOKEN words, or being nothing but a
    ///   stage-direction/gesture. Engine-free: no dependency on the mod, the transcript, or the session.
    /// </summary>
    public static class WitnessSpeechDetector
    {
        // A balanced *...* span is a stage direction (a gesture, an expression). Non-greedy inner match so
        // two separate spans on one line ("*smirks* ... *taps fingers*") are stripped individually rather
        // than swallowed as one span running from the first '*' to the last.
        private static readonly Regex StageDirection = new Regex(@"\*[^*]*\*", RegexOptions.Compiled);

        /// <summary>
        ///   True when <paramref name="reactionText" /> contains real spoken words (bare or quoted) once any
        ///   balanced stage-direction spans are stripped away. False for a pure gesture, or for null/whitespace.
        ///   Deliberately conservative: an unmatched stray '*' around real words still counts as spoken, since
        ///   only a remainder with NO word characters at all is classified as gesture-only.
        /// </summary>
        public static bool HasSpokenWords(string reactionText)
        {
            if (string.IsNullOrWhiteSpace(reactionText)) return false;

            string remainder = StageDirection.Replace(reactionText, "");

            foreach (char c in remainder)
                if (char.IsLetterOrDigit(c))
                    return true;

            return false;
        }
    }
}
