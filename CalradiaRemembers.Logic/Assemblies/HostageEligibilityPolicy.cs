// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "give_hostage", v1 "invite
// d'honneur"): who among a Parley leader's own relatives may ever be offered as a hostage. COUNCIL_ACTIONS.md's
// own CONDITIONS are explicit: "l'otage est parent direct du promettant, vivant, libre, majeur, pas chef de
// clan, pas heritier unique (sinon enjeu de succession vanilla trop lourd)". Kept pure (no Hero/Clan dependency)
// so the mod's own HostageCandidateResolver need only flatten live engine state into these five facts; every
// combination of true/false is then unit-tested here directly, mirroring PledgeAgainstPolicy's own
// engine-agnostic split.
//
// Design call on "sole heir" (COUNCIL_ACTIONS.md leaves the exact mechanical test to the implementer): vanilla
// Bannerlord has no explicit "heir" flag, only a succession algorithm that runs when a clan leader dies. This
// build approximates it the same way ExpulsionPolicy/FiefTransferPolicy already approximate soft vanilla rules
// they cannot query directly: IsSoleHeir is true when the candidate is the pledger's clan's ONLY OTHER alive
// adult member besides the pledger themselves (computed by the caller, CalradiaRemembers.Assemblies.
// HostageCandidateResolver, from Clan.Heroes). Removing that one remaining kinsman would leave the clan a single
// death away from extinction, exactly the succession risk Gabriel's own condition guards against; a clan with
// two or more other adult members never trips this guard for any of them.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The flattened facts about one candidate relative needed to judge them hostage-eligible.</summary>
    public readonly struct HostageCandidateFacts
    {
        public HostageCandidateFacts(bool isAlive, bool isFree, bool isAdult, bool isClanChief, bool isSoleHeir)
        {
            IsAlive = isAlive;
            IsFree = isFree;
            IsAdult = isAdult;
            IsClanChief = isClanChief;
            IsSoleHeir = isSoleHeir;
        }

        public bool IsAlive { get; }

        /// <summary>Not already held prisoner (by anyone) or otherwise unfree.</summary>
        public bool IsFree { get; }

        /// <summary>Not a vanilla child (Hero.IsChild): COUNCIL_ACTIONS.md's own "pas mineur".</summary>
        public bool IsAdult { get; }

        /// <summary>Leads a clan (their own, or any other) as its chief: COUNCIL_ACTIONS.md's own "pas chef de clan".</summary>
        public bool IsClanChief { get; }

        /// <summary>The pledger's clan's own ONLY other alive adult member: COUNCIL_ACTIONS.md's own "pas heritier unique".</summary>
        public bool IsSoleHeir { get; }
    }

    /// <summary>Pure policy: whether a candidate relative may ever be offered as a give_hostage.</summary>
    public static class HostageEligibilityPolicy
    {
        /// <summary>
        ///   True only when EVERY one of COUNCIL_ACTIONS.md's own conditions holds: alive, free, adult, not a
        ///   clan chief, and not the pledger's clan's sole remaining heir. Any single failing fact refuses the
        ///   whole candidate; this is the SAME predicate both the offering fact (teaching gate) and the lift's
        ///   own re-validation (CouncilLift.SettleGiveHostage) must read, so a candidate proven eligible at offer
        ///   time can only ever be refused at the lift by the world having changed in between, never by the two
        ///   sides disagreeing on the rule itself.
        /// </summary>
        public static bool IsEligible(HostageCandidateFacts facts)
            => facts.IsAlive && facts.IsFree && facts.IsAdult && !facts.IsClanChief && !facts.IsSoleHeir;
    }
}
