// Code written by Gabriel Mailhot, 22/06/2026.
// The negotiation framework's "declare war" deed (Phase 2): an NPC demands the PLAYER declare war on a
// faction ("rid me of the Vlandians and I am yours"). Self-balancing — it is the player's own act with
// the player's own consequences. Pure so the verification rule stays tested: the deed is satisfied ONLY
// when the PLAYER's faction is the aggressor against the demanded faction — never when war is declared
// ON the player (that is the opposite act).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a war declaration satisfies a deliver-war-on-faction deed.</summary>
    public static class WarDeedMatcher
    {
        /// <summary>
        ///   True when a war declaration satisfies the deed: the player's faction is the AGGRESSOR
        ///   (<paramref name="aggressorFactionId"/>) and the demanded faction is the DEFENDER. A war the
        ///   player merely got dragged into as the defender does not count — the deed is the player's
        ///   own declaration.
        /// </summary>
        public static bool IsSatisfied(
            string deedTargetFactionId,
            string aggressorFactionId,
            string defenderFactionId,
            string playerFactionId)
        {
            if (string.IsNullOrWhiteSpace(deedTargetFactionId) || string.IsNullOrWhiteSpace(playerFactionId))
                return false;

            bool playerIsAggressor = string.Equals(aggressorFactionId, playerFactionId, StringComparison.OrdinalIgnoreCase);
            bool targetIsDefender  = string.Equals(defenderFactionId, deedTargetFactionId, StringComparison.OrdinalIgnoreCase);

            return playerIsAggressor && targetIsDefender;
        }
    }
}
