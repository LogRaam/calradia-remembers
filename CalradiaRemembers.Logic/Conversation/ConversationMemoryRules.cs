// Code written by Gabriel Mailhot, 03/07/2026.
// Whether a closing conversation deserves a fallback memory when the LLM emitted no [EVENT]. The old gate
// (two completed player turns) silently dropped SINGLE-exchange conversations that carried real substance:
// a player reported an NPC forgetting "this is done, Lucon took it very well... we're free!" because they
// said it in one turn and left. One substantial turn is a conversation; a drive-by "hello" is not.

namespace CalradiaRemembers.Logic.Conversation
{
    /// <summary>
    ///   The rule for recording a fallback conversation memory at window close (used only when the LLM
    ///   emitted no [EVENT] of its own): two completed player turns always qualify, and a SINGLE turn
    ///   qualifies when the player's own words carried enough substance to be worth remembering.
    /// </summary>
    public static class ConversationMemoryRules
    {
        /// <summary>Characters of player text at which a single-turn exchange counts as substantial. Tuning.</summary>
        public const int SubstantialSoloTurnChars = 40;

        /// <summary>
        ///   True when the closing conversation deserves a fallback memory: at least two completed player
        ///   turns, or one turn whose total player text reaches <see cref="SubstantialSoloTurnChars" />.
        /// </summary>
        public static bool DeservesFallbackMemory(int completedPlayerTurns, int playerCharsTotal)
        {
            if (completedPlayerTurns >= 2) return true;

            return completedPlayerTurns == 1 && playerCharsTotal >= SubstantialSoloTurnChars;
        }
    }
}
