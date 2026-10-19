// Code written by Gabriel Mailhot, 22/06/2026.
// The negotiation framework's "declare war" deed (Phase 2): an NPC demands the PLAYER declare war on a
// faction ("rid me of the Vlandians and I am yours"). Self-balancing — it is the player's own act with
// the player's own consequences. Pure so the verification rule stays tested: the deed is satisfied ONLY
// when the PLAYER's faction is the aggressor against the demanded faction — never when war is declared
// ON the player (that is the opposite act).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.War
{
    /// <summary>Decides whether a war declaration satisfies a deliver-war-on-faction deed.</summary>
    public static class WarDeedMatcher
    {
        /// <summary>
        ///   True when a war declaration satisfies the deed: the player's faction is the AGGRESSOR
        ///   of the war and the demanded faction is the DEFENDER. A war the
        ///   player merely got dragged into as the defender does not count — the deed is the player's
        ///   own declaration.
        ///   <para>
        ///     Takes <see cref="WarSides" /> and the two ids it is measured against rather than four bare
        ///     strings (2026-09-10). Swap the deed's target with the player's own faction in the old
        ///     signature and the deed reads as satisfied by the wrong war entirely. The blank-id guard and
        ///     the case-insensitive comparison moved onto the type, which KeepPeaceBreachPolicy was
        ///     duplicating line for line.
        ///   </para>
        /// </summary>
        public static bool IsSatisfied(WarSides war, string deedTargetFactionId, string playerFactionId)
        {
            return war.AggressorIs(playerFactionId) && war.DefenderIs(deedTargetFactionId);
        }
    }
}
