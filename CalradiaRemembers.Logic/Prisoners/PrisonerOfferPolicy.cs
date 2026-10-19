// Code written by Gabriel Mailhot, 25/08/2026.
// New letter reason PrisonerOffer: a lord writes offering to buy a prisoner the player holds. This pure policy
// decides the GOLD the lord offers, clamped into the same honest ransom band a face-to-face buy_prisoner trade
// uses (PrisonerTradePolicy), so the letter names a fair, non-absurd figure the Accept button can then settle.
// A lord with a real personal stake in the captive (their own kin, clansman, or ally) opens above the base ransom;
// an opportunist merely at the base. Kept pure so the figure is unit-tested, not rolled in the letter collector.

namespace CalradiaRemembers.Logic.Prisoners
{
    /// <summary>The gold a PrisonerOffer letter names for the captive (clamped to the honest ransom band).</summary>
    public static class PrisonerOfferPolicy
    {
        /// <summary>A lord with a personal stake (kin/clan/ally of the captive) opens above the base ransom; still clamped to the ceiling.</summary>
        public const double PersonalStakePremium = 1.5;

        /// <summary>
        ///   TUNE: what each envoy already sent away adds to the offer, in ransom anchors (fkasad, 12/10/2026: "if they
        ///   really want their guy back, they could at least raise the price a bit"). 0.5: a kinsman's 1.5 becomes 2.0.
        /// </summary>
        public const double RaisePerEnvoyTurnedAway = 0.5;

        /// <summary>
        ///   The gold the lord offers for the captive: the game's ransom anchor, lifted by
        ///   <see cref="PersonalStakePremium" /> when the lord has a personal stake and by <see cref="RaisePerEnvoyTurnedAway" />
        ///   for each envoy about this captive the player already sent away, then clamped into the honest
        ///   band (<see cref="PrisonerTradePolicy.ClampPrice" />) so a letter never names an absurd sum.
        /// </summary>
        public static int OfferAmount(int ransomAnchor, bool personalStake, int envoysTurnedAway = 0)
        {
            int anchor = ransomAnchor > 0 ? ransomAnchor : PrisonerTradePolicy.FallbackAnchor;
            double premium = (personalStake ? PersonalStakePremium : 1.0) + RaisePerEnvoyTurnedAway * System.Math.Max(0, envoysTurnedAway);
            int raw = (int) System.Math.Min(int.MaxValue, anchor * premium);

            return PrisonerTradePolicy.ClampPrice(raw, anchor);
        }
    }
}
