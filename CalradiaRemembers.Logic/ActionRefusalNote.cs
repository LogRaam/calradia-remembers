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
//
// bdionysis80 (Discord, 21/11/2026), Revenue Farming for Queen Taarna: he confirmed "Give Queen Taarna 2406 coins?
// [Y/n]", the coin moved, and the queen went on asking whether he had collected the taxes at all. Two notes were
// missing or wrong. A CONFIRMED deed left no note, so the model never learned the player had pressed Y (the line
// "You give Queen Taarna 2406 denars" reached only the window). And the same turn the model had invented a verb the
// game does not have; its refusal note said only "the game refused that ... it did not happen", naming no deed, and
// the queen applied it to the coin she had just been paid. So a carried-out confirmation now says it is done, and an
// invented verb's note names the verb and says the rest of the turn stands.

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

        /// <summary>
        ///   The note for an [ACTION] whose type the game does not have at all. It names that one verb, so the model
        ///   cannot pin the refusal on another deed of the same turn (a payment that really landed), and says the
        ///   rest of the turn stands. A blank verb still gets a note that limits itself to "that one deed".
        /// </summary>
        public static string ForUnknownVerb(string verb)
        {
            string name = verb?.Trim();
            string which = string.IsNullOrEmpty(name) ? "one deed you named" : $"the deed '{name}'";

            return $"(Out of character: the game has no such deed as {which}, so that one thing did not happen. "
                   + "Everything else in this exchange stands exactly as the game reported it: a payment, a gift or "
                   + "any other deed it carried out IS done. Do not explain this within the world.)";
        }

        /// <summary>
        ///   The note for a deed the player CONFIRMED and the game carried out, or null when there is nothing to say
        ///   (the deed was refused, which <see cref="For" /> covers, or the game gave no account of it). The model
        ///   proposed the deed and never saw the player's answer; without this it does not know the deed is done.
        /// </summary>
        public static string ForConfirmedDeed(bool wasRefused, string outcomeMessage)
        {
            if (wasRefused) return null;

            string account = outcomeMessage?.Trim();
            if (string.IsNullOrEmpty(account)) return null;

            return $"(Out of character: the player agreed, and the game carried it out. As the game told the player: "
                   + $"\"{account}\" It is done: treat it as done, and do not ask again whether it happened.)";
        }
    }
}
