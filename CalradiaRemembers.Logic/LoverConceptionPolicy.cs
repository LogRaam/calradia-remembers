// Code written by Gabriel Mailhot, 10/08/2026.
// Open relationship (Inc 3c): a tryst may conceive a child by the LOVER. This pure policy owns the one decision
// the mod-side tryst pass must not fork: WHETHER a fertile partner conceives by her lover on a given tryst, a
// modest per-tryst chance gated on an active affair and the mother's fertility. The engine facts (is the affair
// active, is the mother fertile via PregnancyService.IsEligible, the random roll) are supplied by the host; the
// RULE and its one tuning knob live here, named and tested. On a conception the resulting child is attributed to
// the lover (its genetic father), never the player-husband: that attribution is the mod-side birth pipeline's job.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure rule for a lover-child being conceived on a tryst: the modest per-tryst chance that a fertile
    ///   partner in an active affair falls pregnant by her lover. Consumed by the mod's tryst pass; the host
    ///   supplies the engine facts (affair active, mother fertile, the roll), this holds the decision.
    /// </summary>
    public static class LoverConceptionPolicy
    {
        /// <summary>The modest per-tryst chance a fertile partner conceives by her lover.</summary>
        public const int ChancePercent = 20;

        /// <summary>
        ///   Whether a partner conceives by her lover on this tryst. True only when the affair is ACTIVE
        ///   (<paramref name="affairActive" />, a lover is taken), the mother is FERTILE
        ///   (<paramref name="motherFertile" />, the host's PregnancyService.IsEligible read), and
        ///   <paramref name="rollPercent" /> (0..99) falls under <see cref="ChancePercent" />. The host rolls the
        ///   percent (via MBRandom) so the decision stays pure and testable.
        /// </summary>
        public static bool ShouldConceive(bool affairActive, bool motherFertile, int rollPercent)
        {
            if (!affairActive) return false;
            if (!motherFertile) return false;
            return rollPercent < ChancePercent;
        }
    }
}
