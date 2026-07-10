// Code written by Gabriel Mailhot, 09/07/2026.
// Player report (v1.29.0): a MALE player completed a quest for a MALE lord and the lord's WIFE sent jealous
// threats. Root cause (design ruling, Gabriel 2026-07-09): a male player is no threat to her marriage, the mod's
// romance model (like its marriage system) is opposite-sex, so a same-sex pair can never read as courtship. On
// top of that, the model sometimes stamps warm gratitude as a [EVENT] flirt; "words nudge, deeds prove": spoken
// flirtation only counts once it is a PATTERN, and a flirt stamped in the same breath as a quest completion is
// mislabeled gratitude, never courtship. Intimacy is a deed and always counts (for a plausible pair).

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure gate deciding whether a recorded romantic act is plausible COURTSHIP worth a jealous reaction.</summary>
    public static class JealousyTriggerPolicy
    {
        /// <summary>
        ///   TUNE: how many flirt/intimacy events (including today's) the pair must have before spoken
        ///   flirtation reads as a courtship pattern rather than a one-off pleasantry.
        /// </summary>
        public const int FlirtPatternMinimum = 2;

        /// <summary>
        ///   True when the act should provoke a staked third party. <paramref name="isOppositeSexPair" /> is
        ///   whether the player and the courted NPC are of opposite sexes (the mod's romance model, like its
        ///   marriage system, is opposite-sex, so nothing else reads as a rival courtship);
        ///   <paramref name="romanticEventCount" /> is the pair's total recorded flirt/intimacy events including
        ///   the one just stamped; <paramref name="completedQuestThisTurn" /> is true when the same reply
        ///   settled a quest.
        /// </summary>
        public static bool IsPlausibleCourtship(RomanticActTier tier,
                                                bool isOppositeSexPair,
                                                int romanticEventCount,
                                                bool completedQuestThisTurn)
        {
            if (!isOppositeSexPair) return false; // no threat to the marriage: not a rival in this romance model

            if (tier != RomanticActTier.Flirt) return true; // intimacy or marriage: deeds, always count

            if (completedQuestThisTurn) return false; // gratitude at a quest turn-in, mislabeled by the model

            return romanticEventCount >= FlirtPatternMinimum; // words only count once they form a pattern
        }
    }
}
