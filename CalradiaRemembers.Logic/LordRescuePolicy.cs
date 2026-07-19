// Code written by Gabriel Mailhot, 19/07/2026.
// Whether a lord rides to break the captivity the player is sitting in, and WHY. Pure: the host reads the
// world, this decides what it means. Increment 0 of the rescue pillar, deliberately PASSIVE: nothing calls
// it yet, so the thresholds can be read in tests before a single lord changes course.
//
// Ratified with Gabriel 2026-07-18/19, and every boundary below is one of his calls:
//   - A liberation is triggered by CR explicitly, and NOT every time, so that being a prisoner keeps its
//     weight and the captivity event gets to happen at all.
//   - The gate is the captivity having been LIVED, not a number of days. The event is the experience.
//   - A lord with no interest in freeing the player does not come, and the interception is stopped rather
//     than quietly turned into something it is not.
//
// The MOTIVE is returned beside the verdict on purpose. When CR dispatches the rescuer, the intent is
// genuinely known for once, which is the one case where the chronicle may honestly say he came FOR the
// player (see CaptivityReleaseNarrator, which otherwise states only the deed because intent is unknowable).

namespace CalradiaRemembers.Logic
{
    /// <summary>Why a lord rode out, when one did. Ordered by how strongly it binds him.</summary>
    public enum LordRescueMotive
    {
        /// <summary>Nobody rides.</summary>
        None,

        /// <summary>His own clan is in a cage. No further justification is needed or wanted.</summary>
        Kinship,

        /// <summary>A spouse, a lover, an acknowledged courtship.</summary>
        Devotion,

        /// <summary>He wants the captors dead on his own account; freeing the player is the consequence.</summary>
        Vendetta,

        /// <summary>An oath binds them, and a liege or a sworn vassal is not left in a ditch.</summary>
        Fealty,

        /// <summary>
        ///   No bond at all: an honourable lord does not hear that someone was dragged off the road and
        ///   simply ride on. The transcript that started this pillar named exactly this motive.
        /// </summary>
        Duty,

        /// <summary>He thinks well enough of the player to put his own men at risk for them.</summary>
        Regard
    }

    /// <summary>Decides whether one lord mounts a rescue, and on what grounds.</summary>
    public static class LordRescuePolicy
    {
        /// <summary>TUNE: personal regard at or above which a lord rides for the player's own sake.</summary>
        public const int RegardRidesThreshold = 25;

        /// <summary>TUNE: the Honour at or above which a lord rides for a stranger, on duty alone.</summary>
        public const int DutyHonorThreshold = 1;

        /// <summary>
        ///   TUNE: a lord of no honour still rides if he is merciful enough. Kept as a SEPARATE path rather
        ///   than a sum, because pity and principle are different reasons and a lord may have either.
        /// </summary>
        public const int DutyMercyThreshold = 2;

        /// <summary>
        ///   The grounds on which this lord would ride, or <see cref="LordRescueMotive.None" />.
        ///   Order matters only for which motive is REPORTED: any one of them is enough to ride, and the
        ///   first that applies is the truest thing to say about why he came.
        /// </summary>
        public static LordRescueMotive Evaluate(LordRescueFacts facts)
        {
            if (facts == null) return LordRescueMotive.None;

            // The hard gates, all four of them Gabriel's rationing rather than physics.
            if (!facts.CaptivityWasExperienced) return LordRescueMotive.None;
            if (!facts.LordIsFreeToRide) return LordRescueMotive.None;
            if (!facts.WithinRange) return LordRescueMotive.None;
            if (facts.AlreadyAttemptedThisCaptivity) return LordRescueMotive.None;
            if (!facts.CaptorIsBandit) return LordRescueMotive.None;

            if (facts.SharesPlayerClan) return LordRescueMotive.Kinship;
            if (facts.HasRomanticBond) return LordRescueMotive.Devotion;
            if (facts.HoldsGrudgeAgainstCaptor) return LordRescueMotive.Vendetta;
            if (facts.BoundByOath) return LordRescueMotive.Fealty;
            if (facts.PersonalRegard >= RegardRidesThreshold) return LordRescueMotive.Regard;

            // Duty last, and it is the ONLY path open to a lord who owes the player nothing. Deliberately
            // demanding: without a bond it takes a genuinely honourable or genuinely merciful character.
            if (facts.Honor >= DutyHonorThreshold || facts.Mercy >= DutyMercyThreshold)
                return LordRescueMotive.Duty;

            return LordRescueMotive.None;
        }

        /// <summary>True when this lord rides at all. Convenience over <see cref="Evaluate" />.</summary>
        public static bool Rides(LordRescueFacts facts) => Evaluate(facts) != LordRescueMotive.None;

        /// <summary>
        ///   Whether a rescue already under way should be called off (Gabriel 2026-07-19: if the player gets
        ///   free before the rescuer reaches the captors, the rescuer stops). A ride that continues after its
        ///   reason has gone is worse than none: the lord bears down on a player who is no longer in danger,
        ///   which reads as a hostile pursuit rather than a rescue.
        /// </summary>
        /// <param name="playerStillCaptive">The player is still in someone's hands.</param>
        /// <param name="captorPartyStillExists">
        ///   The band being ridden at still exists. Another party may have destroyed it, or it may have
        ///   dispersed; either way there is nothing left to charge.
        /// </param>
        /// <param name="lordStillFreeToRide">He has not since died, been taken, or lost his party.</param>
        public static bool ShouldAbandonRescue(bool playerStillCaptive,
                                               bool captorPartyStillExists,
                                               bool lordStillFreeToRide)
            => !playerStillCaptive || !captorPartyStillExists || !lordStillFreeToRide;
    }
}
