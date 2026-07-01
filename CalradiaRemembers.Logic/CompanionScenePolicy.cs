// Code written by Gabriel Mailhot, 30/06/2026.
// "Stance with teeth" — captive scenes where the victim is one of the player's companions, held alongside them.
// A lord can torment a companion before the player, have his men set upon them, or force the player to do it.
// These scenes seed the player's hatred of the captor (a revenge driver for the rest of the campaign). Pure and
// unit-tested so the odds and variant split stay protected; the scene itself and its consequences are host glue.

namespace CalradiaRemembers.Logic
{
    /// <summary>The shape of a companion-victim captive scene.</summary>
    public enum CompanionSceneKind
    {
        /// <summary>The captor forces the PLAYER to harm their own companion (coerced perpetrator).</summary>
        PlayerCoerced,

        /// <summary>The captor's soldiers set upon the companion before the player (a collective scene).</summary>
        SoldiersAssault,

        /// <summary>The captor torments the companion in person while the player is made to watch.</summary>
        CaptorTorments
    }

    /// <summary>Whether a captive encounter targets a held companion, and which way it plays out.</summary>
    public static class CompanionScenePolicy
    {
        /// <summary>The chance, when companions are held, that an encounter targets one of them instead of the player.</summary>
        public const int CompanionSceneChancePercent = 45;

        // The variant split (tunable): coerce the player, the men assault, or the captor does it himself.
        private const int CoerceUpTo = 35;  // 0..34   -> the player is forced to act
        private const int AssaultUpTo = 70;  // 35..69 -> the captor's men assault; else the captor himself

        /// <summary>
        ///   True when this captive encounter should target a held companion rather than the player. Requires at
        ///   least one companion held alongside the player; a 0..99 roll inside the chance triggers it.
        /// </summary>
        public static bool ShouldTargetCompanion(int heldCompanions, int rollPercent)
            => heldCompanions > 0 && rollPercent >= 0 && rollPercent < CompanionSceneChancePercent;

        /// <summary>Which companion-scene variant to play, from a 0..99 roll.</summary>
        public static CompanionSceneKind PickKind(int rollPercent)
        {
            int r = rollPercent < 0 ? 0 : rollPercent;

            return r < CoerceUpTo
                ? CompanionSceneKind.PlayerCoerced
                : r < AssaultUpTo
                    ? CompanionSceneKind.SoldiersAssault
                    : CompanionSceneKind.CaptorTorments;
        }

        /// <summary>True when the player is the forced perpetrator in this variant (vs a restrained witness).</summary>
        public static bool IsPlayerCoerced(CompanionSceneKind kind) => kind == CompanionSceneKind.PlayerCoerced;

        /// <summary>True when this variant is a collective scene (the captor's men take part).</summary>
        public static bool IsCollective(CompanionSceneKind kind) => kind == CompanionSceneKind.SoldiersAssault;
    }
}
