// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "sponsor_ward"): who among the
// player's own clan companions may ever be taken on as a ward. COUNCIL_ACTIONS.md's own CONDITIONS: "protege =
// compagnon du clan ou wanderer recrutable... un protege n'a qu'un mentor a la fois". This MINIMAL v1 narrows
// that to a companion ALREADY of the player's clan (never an unrecruited wanderer, which would tangle with
// join_party's own hiring flow, a documented follow-up if ever widened). Kept pure (no Hero/Clan dependency) so
// the mod's own WardCandidateResolver need only flatten live engine state into these four facts, mirroring
// HostageEligibilityPolicy's own engine-agnostic split exactly.
//
// ADOPTION WARNING (Kimi's own review, read first): this predicate governs a MENTORSHIP bond only. Nothing here,
// nor anywhere sponsor_ward touches, ever changes Hero.Clan, Hero.Mother/Father, or vanilla succession - a ward
// is never adopted, never becomes an heir, and never leaves their own clan. See CouncilWardBehavior's own header
// for the full mechanic.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>The flattened facts about one candidate companion needed to judge them ward-eligible.</summary>
    public readonly struct WardCandidateFacts
    {
        public WardCandidateFacts(bool isAlive, bool isFree, bool isYoungAdult, bool isAlreadySponsored)
        {
            IsAlive = isAlive;
            IsFree = isFree;
            IsYoungAdult = isYoungAdult;
            IsAlreadySponsored = isAlreadySponsored;
        }

        public bool IsAlive { get; }

        /// <summary>Not held prisoner (by anyone) or otherwise unfree.</summary>
        public bool IsFree { get; }

        /// <summary>Within WardPolicy's own ["MinAge", "MaxAge"] band: this task's own "young companion" gate.</summary>
        public bool IsYoungAdult { get; }

        /// <summary>Already the protege of a live bond (CouncilWardBehavior.IsProtege): COUNCIL_ACTIONS.md's own "un protege n'a qu'un mentor a la fois".</summary>
        public bool IsAlreadySponsored { get; }
    }

    /// <summary>Pure policy: whether a candidate companion may ever be taken on as a sponsor_ward.</summary>
    public static class WardEligibilityPolicy
    {
        /// <summary>
        ///   True only when EVERY fact holds: alive, free, within the young-adult age band, and not already
        ///   sponsored by another mentor. This is the SAME predicate both the offering fact (teaching gate) and
        ///   the lift's own re-validation (CouncilLift.SettleSponsorWard, via CouncilWardBehavior.TryStart) must
        ///   read, so a candidate proven eligible at offer time can only ever be refused at the lift by the world
        ///   having changed in between, never by the two sides disagreeing on the rule itself.
        /// </summary>
        public static bool IsEligible(WardCandidateFacts facts)
            => facts.IsAlive && facts.IsFree && facts.IsYoungAdult && !facts.IsAlreadySponsored;
    }
}
