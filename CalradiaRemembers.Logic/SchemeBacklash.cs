// Code written by Gabriel Mailhot, 21/06/2026.
// v1.3 schemes — the price of taking a hand. Aiding a plot or warning a target is no longer free:
// if an abetted plot's outcome surfaces the player's complicity, the wronged party resents them; a
// warned plotter, exposed, suspects the meddler. Pure so these stakes stay protected by tests.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The relation penalties the player risks for meddling in a scheme. The host tracks who was
    ///   involved and applies the hit; this holds the rule for when — and how much — it costs.
    /// </summary>
    public static class SchemeBacklash
    {
        /// <summary>Caught conspiring against one's OWN realm — high treason, the gravest.</summary>
        public const int AbettorTreasonPenalty = 25;

        /// <summary>Caught betraying a friend (of another realm) — a grave personal treachery.</summary>
        public const int AbettorFriendPenalty = 18;

        /// <summary>Caught moving against someone the player had no quarrel with.</summary>
        public const int AbettorNeutralPenalty = 12;

        /// <summary>Caught moving against an existing enemy — half-expected, so it stings least.</summary>
        public const int AbettorEnemyPenalty = 6;

        /// <summary>What an exposed plotter's regard for the player falls by, suspecting them of the warning.</summary>
        public const int WarnedPlotterResentment = 8;

        /// <summary>How much the betrayed crown (the player's own faction leader) cools on a discovered treason.</summary>
        public const int TreasonCrownPenalty = 10;

        // Relation tiers, matching the rest of the mod's "a friend" / "an enemy" thresholds.
        private const int FriendThreshold = 10;
        private const int EnemyThreshold = -10;

        /// <summary>
        ///   The hit the target's regard for the player takes when an abetted plot reaches an outcome
        ///   that shows the player's hand — it SUCCEEDED (the deed, with the player in it, is known) or
        ///   was DISCOVERED mid-flight. A plot that simply fizzled (Failed without exposure) never
        ///   surfaces the player's role, so it costs nothing. The gravity scales with WHO was wronged:
        ///   betraying one's own realm is treason; betraying a friend is grave; moving against an
        ///   existing enemy is half-expected. Magnitude only; the host negates it.
        /// </summary>
        public static int AbettorPenalty(SchemeState outcome, bool targetSharesPlayerFaction, int playerRelationWithTarget)
        {
            if (outcome != SchemeState.Succeeded && outcome != SchemeState.Discovered) return 0;

            if (targetSharesPlayerFaction) return AbettorTreasonPenalty;            // treason, whatever the personal feud
            if (playerRelationWithTarget >= FriendThreshold) return AbettorFriendPenalty;
            if (playerRelationWithTarget <= EnemyThreshold) return AbettorEnemyPenalty;
            return AbettorNeutralPenalty;
        }
    }
}
