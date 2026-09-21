// Code written by Gabriel Mailhot, 21/06/2026.
// v1.3 schemes — the price of taking a hand. Aiding a plot or warning a target is no longer free:
// if an abetted plot's outcome surfaces the player's complicity, the wronged party resents them; a
// warned plotter, exposed, suspects the meddler. Pure so these stakes stay protected by tests.

#region

using System;

#endregion

using NpcMemoryService.Core.Standing;

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The relation penalties the player risks for meddling in a scheme. The host tracks who was
    ///   involved and applies the hit; this holds the rule for when — and how much — it costs.
    /// </summary>
    public static class SchemeBacklash
    {
        /// <summary>Caught betraying a friend (of another realm) — a grave personal treachery.</summary>
        public const int AbettorFriendPenalty = 18;

        /// <summary>Caught moving against someone the player had no quarrel with.</summary>
        public const int AbettorNeutralPenalty = 12;

        /// <summary>Caught moving against an existing enemy — half-expected, so it stings least.</summary>
        public const int AbettorEnemyPenalty = 6;

        /// <summary>
        ///   How much an exposed plotter's regard for the player falls. A vindictive, calculating lord
        ///   takes the betrayal hardest; a merciful one bears it with more grace.
        ///   Result clamped to [20..40] so the plotter always becomes a clear enemy.
        /// </summary>
        public static int WarnedPlotterResentment(int mercy, int calculating)
            => Math.Max(20, Math.Min(40, 30 - mercy * 3 + calculating * 3));

        /// <summary>
        ///   How much a warned target's regard for the player rises. An honorable lord feels a genuine
        ///   debt; a calculating one suspects the motive and discounts the gift.
        ///   Result clamped to [16..34] so the target always becomes at least a cautious ally.
        /// </summary>
        public static int WarnedTargetGratitude(int honor, int calculating)
            => Math.Max(16, Math.Min(34, 25 + honor * 3 - calculating * 2));

        // Relation tiers, matching the rest of the mod's "a friend" / "an enemy" thresholds.
        private const int FriendThreshold = VanillaRelation.FriendTier;
        private const int EnemyThreshold = VanillaRelation.EnemyTier;

        /// <summary>
        ///   The hit the target's regard for the player takes when an abetted plot reaches an outcome
        ///   that shows the player's hand — it SUCCEEDED (the deed, with the player in it, is known) or
        ///   was DISCOVERED mid-flight. A plot that simply fizzled (Failed without exposure) never
        ///   surfaces the player's role, so it costs nothing. The gravity scales with WHO was wronged:
        ///   betraying a friend is grave; a stranger, moderate; moving against an existing enemy is
        ///   half-expected. Magnitude only; the host negates it.
        /// </summary>
        public static int AbettorPenalty(SchemeState outcome, int playerRelationWithTarget)
        {
            if (outcome != SchemeState.Succeeded && outcome != SchemeState.Discovered) return 0;

            if (playerRelationWithTarget >= FriendThreshold) return AbettorFriendPenalty;
            if (playerRelationWithTarget <= EnemyThreshold) return AbettorEnemyPenalty;
            return AbettorNeutralPenalty;
        }
    }
}
