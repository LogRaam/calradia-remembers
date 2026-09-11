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
        ///   What this turn is, as far as the room's voice is concerned. FOUR booleans in a row of which two
        ///   must hold and two must not, and getting it backwards either silences a room that should speak or
        ///   makes it say everything twice.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class ReactionTurnFacts
        {
           /// <summary>The player's message opened the floor and the lap has not run yet.</summary>
           public bool FloorRequestPending { get; set; }

           /// <summary>
           ///   Only the conversation's host ventriloquizes the room. A reply routed to a witness is that
           ///   witness's own voice already, and the lap is not about to repeat it.
           /// </summary>
           public bool SpeakerIsMain { get; set; }

           /// <summary>A captive scene never opens the floor, so nothing is ever dropped there.</summary>
           public bool IsCaptiveEncounter { get; set; }

           /// <summary>
           ///   Once the conversation is over there is no lap to defer to, so dropping the lines would silence
           ///   the room instead of giving it a better voice.
           /// </summary>
           public bool ConversationEnded { get; set; }
        }

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
        public static bool DropsReactions(ReactionTurnFacts facts)
        {
           if (facts == null) return false;

           bool floorRequestPending = facts.FloorRequestPending;
           bool speakerIsMain = facts.SpeakerIsMain;
           bool isCaptiveEncounter = facts.IsCaptiveEncounter;
           bool conversationEnded = facts.ConversationEnded;

           return floorRequestPending && speakerIsMain && !isCaptiveEncounter && !conversationEnded;
        }
    }
}
