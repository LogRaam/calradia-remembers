// Code written by Gabriel Mailhot, 20/07/2026.
// Pure consent: the stance one member takes on a motion, and whether a whole council carries it. Deterministic
// so the reasons are documented by tests; the LLM only NARRATES the stance the policy already decided. The
// gradient Gabriel asked for lives in AuthorityOf: the same stance blocks a lords' council but merely colours a
// companions' or family council, where the clan head decides alone.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Turns per-member facts into a stance, and a set of stances into a carries/blocked verdict.</summary>
    public static class AssemblyConsentPolicy
    {
        // ── Tuning (TUNABLE: adjust once play-tested) ──────────────────────────

        /// <summary>At or above this regard, a member backs the player willingly even when the motion costs them.</summary>
        public const int WarmRegard = 20;

        /// <summary>Below this regard, a member who is also hurt by the motion turns to open opposition.</summary>
        public const int LoyalRegard = 0;

        /// <summary>Companions and family advise; clan lords hold binding political weight.</summary>
        public static ConsentAuthority AuthorityOf(CouncilScope scope)
            => scope == CouncilScope.ClanLords ? ConsentAuthority.Binding : ConsentAuthority.Advisory;

        public static ConsentStance Stance(ConsentFacts f)
        {
            // Opposition needs BOTH a personal cost and a lack of goodwill: a member hurt by the motion who also
            // does not hold the player in decent regard speaks against it.
            if (f.Stake < 0 && f.RegardToInitiator < LoyalRegard)
                return ConsentStance.Oppose;

            // Support comes from self-interest (the motion helps them) OR loyalty (warm regard carries them past a
            // cost). Either is enough to back it.
            if (f.Stake > 0 || f.RegardToInitiator >= WarmRegard)
                return ConsentStance.Favor;

            // Otherwise the member is indifferent: a neutral motion, or a costly one they tolerate without warmth.
            return ConsentStance.Abstain;
        }

        /// <summary>
        ///   Tallies a council's stances and decides whether the motion carries. An advisory council (companions,
        ///   family) always carries: the clan head decides, the stances are recorded for their relational cost. A
        ///   binding council (clan lords) carries unless opposition outnumbers support; a tie goes to the head.
        ///   Abstentions never count toward blocking.
        /// </summary>
        public static ConsentResult Decide(CouncilScope scope, System.Collections.Generic.IEnumerable<ConsentStance> stances)
        {
            int favor = 0, oppose = 0, abstain = 0;
            if (stances != null)
                foreach (ConsentStance s in stances)
                    switch (s)
                    {
                        case ConsentStance.Favor:
                            favor++;
                            break;
                        case ConsentStance.Oppose:
                            oppose++;
                            break;
                        default:
                            abstain++;
                            break;
                    }

            bool carries = AuthorityOf(scope) == ConsentAuthority.Advisory || favor >= oppose;
            return new ConsentResult(carries, favor, oppose, abstain);
        }
    }
}
