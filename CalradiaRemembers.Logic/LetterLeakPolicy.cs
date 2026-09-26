// Code written by Gabriel Mailhot, 24/08/2026.
// What makes an intercepted letter worth anything to whoever takes it: substance. Since 26/09/2026 the old roll and
// antagonist pick are gone (Gabriel: an intercepted letter with substance ALWAYS becomes a secret, carried in the
// world by CourierSecretBehavior on CourierSecretPolicy's rules); only the substance bar remains here.

namespace CalradiaRemembers.Logic
{
    /// <summary>The substance bar an intercepted letter must clear to be anyone's secret.</summary>
    public static class LetterLeakPolicy
    {
        /// <summary>
        ///   The shortest letter body worth leaking: a one-line note ("meet me at Sargot") carries no exploitable
        ///   secret, so only a letter with real substance (used here in place of a per-letter reason, which
        ///   <c>PlayerLetter</c> does not carry) can fall into the wrong hands.
        /// </summary>
        public const int MinLeakableContentLength = 40;

        /// <summary>True when the letter body has enough substance to be worth leaking.</summary>
        public static bool HasSubstance(string content)
            => !string.IsNullOrWhiteSpace(content) && content.Trim().Length >= MinLeakableContentLength;
    }
}
