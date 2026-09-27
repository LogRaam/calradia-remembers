// Code written by Gabriel Mailhot, 18/07/2026.
// Whether a bridge outcome earns an out-of-character note in the TRANSCRIPT, and what that note says.
// Extracted from ChatViewModel.Exchanges, where it was written straight into the controller and therefore
// had no test at all (the mod is net472, the test project is net10).
//
// The stake: when the bridge refuses an action the prompt let through (an active war, a captivity, a gate),
// the UI line alone tells only the PLAYER. The model celebrated a marriage or a transfer that never
// happened, and unless the refusal reaches the transcript it is replayed as accomplished into every prompt
// rebuilt afterwards and the following turns build on the lie. Same pattern as a declined confirmation
// (ActionConfirmationController.DeclineAction).
//
// And the REASON is a rule, not a story (tashmetu, 27/09/2026): a refusal saying a lord's purse held nothing was
// explained in the world by the model (the player had seized her treasury), cost the player her regard, and became
// her memory. The note now says so, for every refusal at once.

namespace CalradiaRemembers.Logic
{
    /// <summary>Turns a refused action outcome into the note the model must read, or nothing.</summary>
    public static class ActionRefusalNote
    {
        /// <summary>
        ///   The note to record, or null when nothing should be written. <paramref name="wasRefused" /> is
        ///   the bridge's own verdict; a message is only ever appended when there is one, so a refusal the
        ///   bridge could not explain still states plainly that it did not happen.
        /// </summary>
        public static string For(bool wasRefused, string outcomeMessage)
        {
            if (!wasRefused) return null;

            string reason = outcomeMessage?.Trim();

            return string.IsNullOrEmpty(reason)
                ? "(Out of character: the game refused that. It did not happen. Do not treat it as done.)"
                : $"(Out of character: the game refused that: {reason} It did not happen. Do not treat it as done. "
                  + "That reason is a rule of the game, not an event in the story: do not explain it within the world, "
                  + "and do not remember it as something that happened.)";
        }
    }
}
