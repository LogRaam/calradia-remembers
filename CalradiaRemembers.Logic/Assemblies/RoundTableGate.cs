// Code written by Gabriel Mailhot, 11/07/2026.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   The pure decision state of the round-table feature: whether the player's last message opened the floor to
    ///   the whole group, whether the round-table button/UI should be offered, and where the next deterministic lap
    ///   should start. Kept in CalradiaRemembers.Logic so it is unit-testable without the engine; the engine glue
    ///   (running the actual turns) lives in the mod's RoundTableController, which composes this.
    /// </summary>
    public sealed class RoundTableGate
    {
        private bool _requested;

        /// <summary>
        ///   Round-robin start index for the next lap: rotates so, over repeated triggers, participants beyond the
        ///   per-burst cap still get a turn. Read and advanced by the controller's lap.
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        ///   True when the round-table button/UI should be shown: a group is present (witnessCount &gt; 0), the
        ///   scene is not a captive encounter, and the conversation has not ended.
        /// </summary>
        public bool ComputeVisible(int witnessCount, bool isCaptiveEncounter, bool conversationEnded)
            => witnessCount > 0 && !isCaptiveEncounter && !conversationEnded;

        /// <summary>
        ///   Records whether the player's own message opened the floor to the whole group this turn: only outside a
        ///   captive scene, with a group present, and when the text reads as a round-table intent
        ///   (<see cref="RoundTableIntentDetector" />). Overwrites any prior pending request.
        /// </summary>
        public void OnPlayerSend(string text, int witnessCount, bool isCaptiveEncounter)
            => _requested = witnessCount > 0 && !isCaptiveEncounter && RoundTableIntentDetector.Detects(text);

        /// <summary>
        ///   True while a request to open the floor is pending, WITHOUT consuming it. The host reads this to
        ///   suppress the main NPC's ventriloquized [WITNESS_REACTION]s on the triggering turn: the lap is
        ///   about to give those same people a real turn in their own voice, and letting both happen made a
        ///   witness speak twice in a row, the second time possibly contradicting the first (round-table
        ///   audit D7, since the two lines come from two different model calls).
        /// </summary>
        public bool IsRequestPending => _requested;

        /// <summary>
        ///   What this turn is, as far as opening the floor is concerned. Three booleans in a row, one that
        ///   must hold and two that must not, and the same three
        ///   <see cref="VentriloquizedReactionPolicy" /> weighs for the mirror-image decision on the very same
        ///   turn: whether to DROP the reactions this lap is about to replace. They are deliberately two types
        ///   rather than one shared one, because the reaction gate reads a fourth fact this one owns instead of
        ///   observing, the pending request itself.
        ///   <para>
        ///     Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///     practice.
        ///   </para>
        /// </summary>
        public sealed class FloorTurnFacts
        {
            /// <summary>The conversation's host just spoke, which is when the lap may start.</summary>
            public bool SpeakerIsMain { get; set; }

            /// <summary>A captive scene, which never opens the floor.</summary>
            public bool IsCaptiveEncounter { get; set; }

            /// <summary>The conversation is over, so there is no lap left to run.</summary>
            public bool ConversationEnded { get; set; }
        }

        /// <summary>
        ///   Consumes a pending "open the floor" request after the main NPC's reply: returns true (and clears the
        ///   request, so it fires at most once) when a request is pending, the main NPC just spoke, the scene is not
        ///   a captive encounter, and the conversation is still open; false otherwise.
        /// </summary>
        public bool TryConsumeForMainReply(FloorTurnFacts turn)
        {
            if (turn == null) return false;

            bool speakerIsMain = turn.SpeakerIsMain;
            bool isCaptiveEncounter = turn.IsCaptiveEncounter;
            bool conversationEnded = turn.ConversationEnded;

            if (_requested && speakerIsMain && !isCaptiveEncounter && !conversationEnded)
            {
                _requested = false;

                return true;
            }

            return false;
        }
    }
}
