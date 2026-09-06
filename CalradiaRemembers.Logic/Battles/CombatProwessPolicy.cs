// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment: decides how remarkable it is that ONE fighter put ANOTHER down. Gabriel's ruling
// (2026-09-06): a world event is worth it only when the deed is an EXPLOIT, e.g. the player downs an opponent much
// stronger than themselves. This mirrors, at the scale of a single duel, the rule the mod already applies at the
// scale of armies (LetterWeights.BattleVictoryWeight, where a lopsided rout floors to nothing because stomping a
// much weaker enemy is not remarkable). Two axes, because they say different things: PROWESS (could they actually
// fight better than you) and FAME (is this a name the realm knows). Pure, so the thresholds Gabriel tunes stay
// testable and the same ruling feeds both the memory text and how far word of it travels.

namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>Rates a single victory: an exploit, a plain deed, or something nobody would remark on.</summary>
    public static class CombatProwessPolicy
    {
        /// <summary>
        ///   How much the two mobility skills are damped against pure weapon skill. Riding and athletics are real
        ///   but clearly SECONDARY: at a divisor of 2 their sum matched a whole weapon skill, so a fast, unskilled
        ///   fighter outrated a master swordsman (caught by
        ///   GIVEN_mobility_against_weapon_skill_WHEN_scored_THEN_the_weapon_skill_wins). Tuning.
        /// </summary>
        public const int MobilityDivisor = 4;

        /// <summary>How much a level is worth in the prowess score, so a veteran of the same weapon skill still reads as harder. Tuning.</summary>
        public const int LevelWeight = 2;

        /// <summary>How far above you a foe must stand before beating them is an EXPLOIT (1.3 = about a third stronger). Tuning.</summary>
        public const double ExploitRatio = 1.3;

        /// <summary>How far ABOVE the foe you must stand before the win stops being worth remarking on at all. Tuning.</summary>
        public const double StompRatio = 1.3;

        /// <summary>The clan tier at which a name is famous enough that beating its bearer is a story whatever the skills say. Tuning.</summary>
        public const int FamousClanTier = 4;

        /// <summary>
        ///   What this fighter is worth in a fight, from the numbers the game itself fights with: their best weapon
        ///   skill above all, mobility as a real but lesser term, and their level as general seasoning. Never
        ///   negative, and never zero for a live combatant, so it is always safe to compare as a ratio.
        /// </summary>
        public static int Prowess(CombatantFacts f)
        {
            if (f == null) return 1;

            int mobility = (Max(f.Riding, 0) + Max(f.Athletics, 0)) / MobilityDivisor;
            int score = Max(f.BestWeaponSkill, 0) + mobility + Max(f.Level, 0) * LevelWeight;

            return score < 1 ? 1 : score;
        }

        /// <summary>
        ///   How remarkable it is that <paramref name="winner" /> put <paramref name="loser" /> down. A foe well
        ///   above the winner is an EXPLOIT; so is a genuinely famous name, whose defeat travels on reputation
        ///   alone. A foe far BENEATH the winner is Unremarkable (the stomp rule), unless they are famous, because
        ///   the realm still talks when a great name falls. Everything between is a plain Notable deed: worth
        ///   remembering by those who saw it, not worth the realm's gossip.
        /// </summary>
        public static CombatFeatTier Rate(CombatantFacts winner, CombatantFacts loser)
        {
            double winnerProwess = Prowess(winner);
            double loserProwess = Prowess(loser);
            bool loserIsFamous = (loser?.ClanTier ?? 0) >= FamousClanTier;

            if (loserIsFamous) return CombatFeatTier.Exploit;
            if (loserProwess >= winnerProwess * ExploitRatio) return CombatFeatTier.Exploit;
            if (winnerProwess >= loserProwess * StompRatio) return CombatFeatTier.Unremarkable;

            return CombatFeatTier.Notable;
        }

        /// <summary>True when word of this deed may travel beyond those who witnessed it (Gabriel: a world event is for an EXPLOIT).</summary>
        public static bool TravelsAsGossip(CombatFeatTier tier) => tier == CombatFeatTier.Exploit;

        private static int Max(int a, int b) => a > b ? a : b;
    }
}
