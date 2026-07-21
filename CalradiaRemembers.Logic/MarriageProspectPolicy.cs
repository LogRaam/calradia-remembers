// Code written by Gabriel Mailhot, 21/07/2026.
// Ratified 2026-07-21. A house OFFERING one of its own in marriage is not the same question as whether two
// people MAY wed, and the two had been collapsed into one rule. Marriage itself is a contract between two
// individuals, so it carries no age gap (see MarriageEligibility). But a match a clan head proposes
// unprompted is a political instrument: it is sought as a LONG-TERM alliance, which wants years ahead of
// it, and the young kin it offers are the ones with the least standing to refuse the arrangement made for
// them. So a spontaneous offer aims young, while a player who asks for the hand of someone older is still
// free to, and is answered by MarriageEligibility alone.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Whether a house would put a given kin forward as a marriage prospect of its own accord. Narrower
    ///   than <see cref="MarriageEligibility" />, which decides only whether a marriage is LAWFUL.
    /// </summary>
    public static class MarriageProspectPolicy
    {
        /// <summary>
        ///   TUNE: the oldest kin a house offers unprompted. Sits inside the fertility window that closes at
        ///   45 (<see cref="ConceptionResolver.MaxFertileAge" />), so an offered match can still carry the
        ///   dynastic point of the alliance, heirs. Lowering it further starves the offer lists of small
        ///   clans, whose leader may have only a few living kin at all.
        /// </summary>
        public const float MaxProspectAge = 30f;

        /// <summary>
        ///   True when a house would name this kin as a prospect: lawful to wed at all, and young enough
        ///   for the long alliance such a match is meant to buy.
        /// </summary>
        public static bool IsOfferableProspect(
            bool candidateAlive,
            bool candidateHasSpouse,
            bool candidateIsFemale,
            bool playerIsFemale,
            float candidateAge,
            float playerAge)
        {
            if (!MarriageEligibility.IsMarriageable(
                   candidateAlive, candidateHasSpouse, candidateIsFemale, playerIsFemale, candidateAge, playerAge))
                return false;

            return candidateAge <= MaxProspectAge;
        }
    }
}
