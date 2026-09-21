// Code written by Gabriel Mailhot, 25/07/2026.
// The consent half of the "matchmaker" feature: the player arranges a marriage between a member of
// their OWN clan and a noble of ANOTHER clan (the player is neither spouse). Hard eligibility (age,
// kinship, marital status) and the actual wedding are handled elsewhere; this file answers ONE question:
// does the target family consent to the match. Deliberately a systems-driven policy, not an LLM
// judgment call: a player report (CarolusIV, Nexus, 2026-07-25) noted their local model "gets confused
// a lot" and must not be talkable into an unwarranted match.

using NpcMemoryService.Core.Standing;

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The outcome of asking a noble family whether they consent to an arranged cross-clan match.
    /// </summary>
    public enum ArrangedMatchConsent
    {
        /// <summary>The target family consents to the match.</summary>
        Agreed,

        /// <summary>
        ///   The "other" noble is actually of the player's own clan: this is not the kind of
        ///   arrangement this feature covers (it is cross-clan only).
        /// </summary>
        NotCrossClan,

        /// <summary>The family declines: the player's standing does not warrant the match.</summary>
        RefusedStanding
    }

    /// <summary>
    ///   The facts an arranged-match decision needs. Everything the engine must resolve (which hero is
    ///   which, live regard, clan tiers) before handing off to the pure policy below.
    /// </summary>
    public readonly struct ArrangedMatchFacts
    {
        /// <summary>True when the noble being married in is NOT of the player's own clan.</summary>
        public bool TargetIsOfAnotherClan { get; init; }

        /// <summary>The target clan head's PERSONAL regard for the player (CR's own ledger).</summary>
        public int TargetFamilyRegard { get; init; }

        /// <summary>The player's clan tier.</summary>
        public int PlayerClanTier { get; init; }

        /// <summary>The target noble's clan tier.</summary>
        public int TargetClanTier { get; init; }
    }

    /// <summary>
    ///   Decides whether a target family consents to an arranged cross-clan marriage. Pure and
    ///   systems-driven on purpose: the LLM narrates the outcome, it never decides it, so a weak or
    ///   easily-led model cannot talk a family into a match it has no reason to accept.
    /// </summary>
    public static class ArrangedMatchPolicy
    {
        /// <summary>
        ///   Regard at or above which a family consents to the match freely, regard being the mod's own
        ///   personal-regard ledger for the target clan's head. Tunable; Gabriel adjusts by feel.
        /// </summary>
        public const int ConsentRegardThreshold = Regard.ConfidingTier;

        /// <summary>
        ///   Decides consent. A family weighs two things: how much they think of the player (regard),
        ///   and whether the match itself is a gain regardless of regard (marrying their kin UP into a
        ///   higher-tier clan). Either is enough on its own; absent both, they have no reason to hand
        ///   over one of their own to a stranger of no greater standing.
        /// </summary>
        public static ArrangedMatchConsent Decide(ArrangedMatchFacts facts)
        {
            if (!facts.TargetIsOfAnotherClan) return ArrangedMatchConsent.NotCrossClan;
            if (facts.TargetFamilyRegard >= ConsentRegardThreshold) return ArrangedMatchConsent.Agreed;
            if (facts.PlayerClanTier > facts.TargetClanTier) return ArrangedMatchConsent.Agreed;
            return ArrangedMatchConsent.RefusedStanding;
        }
    }
}
