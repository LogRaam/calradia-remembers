// Code written by Gabriel Mailhot, 09/07/2026.
// Player report (v1.29.0): a MALE player completed a quest for a MALE lord and the lord's WIFE sent jealous
// threats. Root cause (design ruling, Gabriel 2026-07-09): a pair that cannot be a real courtship is no threat
// to anyone's marriage. Since v1.30.x orientation is authorable, so "cannot be" is decided by the courted NPC's
// ORIENTATION (host-computed via RomanticProfile.IsCompatibleWith): the default world stays opposite-sex, but a
// deliberately enabled same-sex romance is a real courtship and rouses jealousy like any other. On top of that,
// the model sometimes stamps warm gratitude as a [EVENT] flirt; "words nudge, deeds prove": spoken flirtation
// only counts once it is a PATTERN, and a flirt stamped in the same breath as a quest completion is mislabeled
// gratitude, never courtship. Intimacy is a deed and always counts (for a plausible pair).

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure gate deciding whether a recorded romantic act is plausible COURTSHIP worth a jealous reaction.</summary>
    public static class JealousyTriggerPolicy
    {
        /// <summary>
        ///   TUNE: how many flirt/intimacy events (including today's) the pair must have before spoken
        ///   flirtation reads as a courtship pattern rather than a one-off pleasantry. Raised 2 -&gt; 3
        ///   (player report): the model stamps warm small talk as [EVENT] flirt so readily that two
        ///   stamps still did not discriminate a real courtship from an innocent conversation.
        /// </summary>
        public const int FlirtPatternMinimum = 3;

        /// <summary>
        ///   True when the act should provoke a staked third party. <paramref name="isPlausiblePair" /> is
        ///   whether the player and the courted NPC could truly be a courtship, decided by the courted NPC's
        ///   orientation vs the player's gender (host computes it via <c>RomanticProfile.IsCompatibleWith</c>;
        ///   the default world is opposite-sex, an authored orientation can make a same-sex pair real);
        ///   <paramref name="romanticEventCount" /> is the pair's total recorded flirt/intimacy events including
        ///   the one just stamped; <paramref name="completedQuestThisTurn" /> is true when the same reply
        ///   settled a quest.
        /// </summary>
        public static bool IsPlausibleCourtship(RomanticActTier tier,
                                                bool isPlausiblePair,
                                                int romanticEventCount,
                                                bool completedQuestThisTurn)
        {
            if (!isPlausiblePair) return false; // no threat to anyone: this pair cannot be a courtship

            if (tier != RomanticActTier.Flirt) return true; // intimacy or marriage: deeds, always count

            if (completedQuestThisTurn) return false; // gratitude at a quest turn-in, mislabeled by the model

            return romanticEventCount >= FlirtPatternMinimum; // words only count once they form a pattern
        }
    }
}
