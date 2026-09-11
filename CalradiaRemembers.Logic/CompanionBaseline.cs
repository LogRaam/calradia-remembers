// Code written by Gabriel Mailhot, 13/07/2026.
// Serving a great house is an HONOUR (design call, 2026-07-13): the happiness a companion settles at is not a
// fixed number, it rises with the standing of the clan they serve. A companion of a legendary clan is content
// simply to belong, and needs no courting; one in a nameless upstart clan must be won a little harder. This is
// what stops the player from spending their evenings nursing morale (player report, SuperGraham339: companions
// sliding into discontent no matter what he promised, until he switched the whole system off).

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The happiness a companion drifts toward, set by the prestige of the clan they serve. It is the
    ///   equilibrium of <see cref="CompanionHappiness.DriftToBaseline(CompanionHappiness.HappinessDrift)" />: with nothing else acting
    ///   on them, this is where a companion comes to rest.
    /// </summary>
    public static class CompanionBaseline
    {
        /// <summary>Where a companion of a clan with no standing at all (tier 0) settles: the edge of contentment.</summary>
        public const int NoStanding = 58;

        /// <summary>How much each clan tier lifts the resting happiness. Tunable.</summary>
        public const int PerTier = 4;

        /// <summary>Bounds, so an unexpected tier (a mod, a negative) can never produce a nonsense baseline.</summary>
        public const int Min = 56;

        public const int Max = 82;

        /// <summary>
        ///   The resting happiness for a companion serving a clan of this tier. Tier 0 rests at
        ///   <see cref="NoStanding" /> (barely content); a tier-6 house rests at <see cref="Max" /> (devoted,
        ///   they are proud to be counted among you).
        /// </summary>
        public static int From(int clanTier)
        {
            int baseline = NoStanding + clanTier * PerTier;

            if (baseline < Min) return Min;
            if (baseline > Max) return Max;

            return baseline;
        }
    }
}
