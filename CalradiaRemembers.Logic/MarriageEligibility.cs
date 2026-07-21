// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod's MarriageProspectResolver: the rule for whether two people could wed. Pure
// so its thresholds (adult age, opposite-sex only), shared by the prompt listing, the bridge's marry
// re-validation, and love-match eligibility, and silent when they drift, stay protected by tests.
//
// 2026-07-21 (Gabriel, after a player report): the age GAP was removed from this rule. A marriage is a
// CONTRACT between two individuals, so age governs only whether each is an adult, never how far apart
// they are. Age remains a real constraint on BIOLOGY, which is a separate question answered separately
// by ConceptionResolver (fertility 18 to 45, women only), and on what a house spontaneously OFFERS,
// answered by MarriageProspectPolicy. Neither belongs in the legality of the contract itself.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Whether a candidate could marry the player under Calradia's rules: alive, unwed, adult, and of
    ///   the opposite sex (the engine marries opposite-sex only). The host reads the Hero state; this
    ///   holds the actual rule and its tuning.
    /// </summary>
    public static class MarriageEligibility
    {
        /// <summary>Minimum marriage age — Bannerlord adults at 18.</summary>
        public const float MinMarriageAge = 18f;

        /// <summary>
        ///   The regard threshold that makes a love-match marriage conceivable: EITHER the mod's own
        ///   personal-regard ledger (<c>NpcProfile.ReputationWithPlayer</c>) OR the live vanilla clan
        ///   relation (<c>Hero.GetRelation</c>) reaching this value. Shared by
        ///   <c>EncounterContextBuilder.ResolveLoveMatchEligible</c> (teaches the action) and
        ///   <c>BannerlordGameStateBridge.ExecuteMarry</c> (re-validates before sealing it) so the two
        ///   gates can never drift apart. Broadened from personal-regard-only after a player report:
        ///   players who built vanilla relation the ordinary way (gifts, favors, quests) expected that
        ///   to count toward a match, not just the mod's own hidden ledger.
        /// </summary>
        public const int MarriageEligibilityThreshold = 50;

        /// <summary>
        ///   True when the candidate could wed the player: alive and unwed, adult, opposite sex. The
        ///   player's own age is accepted for the caller's convenience and deliberately unused: how far
        ///   apart two adults are is not a bar to a contract they both enter.
        /// </summary>
        public static bool IsMarriageable(
            bool candidateAlive,
            bool candidateHasSpouse,
            bool candidateIsFemale,
            bool playerIsFemale,
            float candidateAge,
            float playerAge)
        {
            if (!candidateAlive || candidateHasSpouse) return false;
            if (candidateIsFemale == playerIsFemale) return false;
            return candidateAge >= MinMarriageAge;
        }
    }
}
