// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod's MarriageProspectResolver: the rule for whether two people could wed. Pure
// so its thresholds (adult age, the plausible age gap, opposite-sex only) — shared by the prompt
// listing, the bridge's marry re-validation, and love-match eligibility, and silent when they drift —
// stay protected by tests.

using System;

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Whether a candidate could marry the player under Calradia's rules: alive, unwed, adult, of
    ///   the opposite sex (the engine marries opposite-sex only), and within a believable age gap.
    ///   The host reads the Hero state; this holds the actual rule and its tuning.
    /// </summary>
    public static class MarriageEligibility
    {
        /// <summary>Minimum marriage age — Bannerlord adults at 18.</summary>
        public const float MinMarriageAge = 18f;

        /// <summary>Widest age difference (years) treated as a plausible match.</summary>
        public const float MaxAgeGap = 25f;

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
        ///   True when the candidate could wed the player: alive and unwed, adult, opposite sex, and
        ///   no further apart in age than <see cref="MaxAgeGap" />.
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
            if (candidateAge < MinMarriageAge) return false;
            return Math.Abs(candidateAge - playerAge) <= MaxAgeGap;
        }
    }
}
