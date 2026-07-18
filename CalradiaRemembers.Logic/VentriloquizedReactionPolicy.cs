// Code written by Gabriel Mailhot, 18/07/2026.
// Whether the main NPC's [WITNESS_REACTION] lines are dropped on the turn the player opened the floor
// (round-table audit D7). Extracted from ChatViewModel.Exchanges, where it was an inline condition in the
// controller and therefore untestable (the mod is net472, the test project is net10).
//
// The fault it guards: with a floor request pending, the round-table lap that follows gives each of those
// people a real turn in their OWN voice. Showing the main NPC's ventriloquized version of them first made a
// witness speak twice in a row, and since the two lines come from two different model calls the second could
// flatly contradict the first. The lap is the better voice, so the ventriloquized lines are the ones to go.

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides when a speaker's witness reactions must yield to the round-table lap.</summary>
    public static class VentriloquizedReactionPolicy
    {
        /// <summary>
        ///   True when this turn's emitted witness reactions must be discarded rather than voiced.
        /// </summary>
        /// <param name="floorRequestPending">The player's message opened the floor and the lap has not run yet.</param>
        /// <param name="speakerIsMain">
        ///   Only the conversation's host ventriloquizes the room. A reply routed to a witness is that
        ///   witness's own voice already, and the lap is not about to repeat it.
        /// </param>
        /// <param name="isCaptiveEncounter">A captive scene never opens the floor, so nothing is ever dropped there.</param>
        /// <param name="conversationEnded">
        ///   Once the conversation is over there is no lap to defer to, so dropping the lines would silence
        ///   the room instead of giving it a better voice.
        /// </param>
        public static bool DropsReactions(bool floorRequestPending,
                                          bool speakerIsMain,
                                          bool isCaptiveEncounter,
                                          bool conversationEnded)
            => floorRequestPending && speakerIsMain && !isCaptiveEncounter && !conversationEnded;
    }
}
