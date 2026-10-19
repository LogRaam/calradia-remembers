// Code written by Gabriel Mailhot, 25/06/2026.
// Bastards (design layer) — whether a grown bastard presses a claim on the FATHER's clan, or follows the
// MOTHER's grander dynasty and covets nothing of it. Decided 2026-06-25: the claim is gated on the relative
// prestige of the two houses (the child of a ruling empress follows her line, not a minor father's clan).
// Pure so the rule — and its tuning — stays protected by tests.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Bastards
{
    /// <summary>How a grown bastard regards the FATHER's clan.</summary>
    public enum BastardClaimVerdict
    {
        /// <summary>The bastard presses a claim on the father's clan — a natural enemy in the making.</summary>
        PressesClaim,

        /// <summary>The bastard follows the mother's grander house and covets nothing of the father's.</summary>
        FollowsMotherDynasty
    }

    /// <summary>
    ///   The rule for a grown bastard's disposition toward the FATHER's clan. A child of a more prestigious
    ///   maternal house (e.g. a ruling empress) follows that dynasty and presses no claim on the father's
    ///   lesser clan; only when the maternal house is no grander does the bastard have a motive to claim it.
    ///   Prestige is an abstract score so the policy stays engine-agnostic — the host maps a clan to it.
    /// </summary>
    public static class BastardPrestigePolicy
    {
        /// <summary>Weight of a clan's tier in its prestige score. Tuning.</summary>
        public const int TierWeight = 100;

        /// <summary>A ruling dynasty towers over an equal-tier vassal house. Tuning.</summary>
        public const int RulingClanBonus = 1000;

        /// <summary>
        ///   An abstract prestige score for a clan: its tier (heavily weighted), its renown, and a large
        ///   bonus when it rules a kingdom. Higher = grander. The host reads tier/renown/ruling from the game.
        ///   Negative renown never subtracts — a struggling house is still at least its tier.
        /// </summary>
        public static int PrestigeScore(int clanTier, int clanRenown, bool isRulingClan)
            => clanTier * TierWeight + Math.Max(0, clanRenown) + (isRulingClan ? RulingClanBonus : 0);

        /// <summary>
        ///   The bastard presses a claim on the father's clan unless the maternal house is strictly more
        ///   prestigious. Equal standing still leaves a motive, so the gate is "maternal greater → no claim".
        /// </summary>
        public static BastardClaimVerdict Resolve(int maternalPrestige, int paternalPrestige)
            => maternalPrestige > paternalPrestige
                ? BastardClaimVerdict.FollowsMotherDynasty
                : BastardClaimVerdict.PressesClaim;
    }
}
