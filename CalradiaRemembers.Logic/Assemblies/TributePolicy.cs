// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "tribute"): the Parley's own
// submission/buy-off motion, pledged by the SEATED ENEMY faction leader against their own coffers, never the
// player's. Unlike grant_stipend's escrow (StipendPolicy: the WHOLE 30-day tranche leaves the player's purse up
// front, so the promise can never fail mid-term), a tribute is PAY-AS-YOU-GO: the payer's own gold is checked
// fresh at every daily tick (CouncilTributeBehavior.Advance), so it CAN run dry mid-term, and the daily tick must
// degrade gracefully (a missed day tolerated up to a grace threshold, never the payer clamped negative) rather
// than fail all at once the way an unfunded escrow would at TryStart.
//
// Author's design calls (adapting COUNCIL_ACTIONS.md's own recipe):
// - Amount [50, 2000] denars/day, the range Kimi's review names directly (the doc's own extra guard, bounding the
//   amount further by a fraction of the payer's own purse, is left to the mod-side offering fact instead of a
//   second clamp here: AnySeatedTributePayerAvailable already only teaches this kind when SOME payer leads a
//   clan of their own, and the lift never trusts the proposed figure over the payer's actual live gold anyway).
// - A FIXED 30-day term (TributeDays), a deliberate simplification versus COUNCIL_ACTIONS.md's own NEGOTIATED
//   10-60 day range: mirroring StipendPolicy.TrancheDays exactly means the LLM proposes only the daily amount,
//   never a second duration parameter to get wrong or clamp.
// - MissedDaysGraceThreshold = 3: COUNCIL_ACTIONS.md's own Kimi review names this figure directly ("3 ticks
//   manques = rupture"), so a run of THREE consecutive missed days breaks the tribute early rather than one bad
//   day ending a promise the payer was otherwise keeping.
namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for tribute: the daily-amount clamp, the fixed term, and the pay-as-you-go affordability check.</summary>
    public static class TributePolicy
    {
        /// <summary>A tribute always runs a FIXED term of 30 days (mirroring StipendPolicy.TrancheDays): the only variable the LLM proposes is the amount per day.</summary>
        public const int TributeDays = 30;

        public const int MinAmountPerDay = 50;
        public const int MaxAmountPerDay = 2000;

        /// <summary>
        ///   Consecutive missed days before a tribute breaks early (COUNCIL_ACTIONS.md's own Kimi review: "3 ticks
        ///   manques = rupture"). Reset to zero by CouncilTributeBehavior.Advance on any successful payment, so a
        ///   payer who recovers after a lean patch is never punished for scattered, non-consecutive misses months
        ///   apart - the stake this constant pins is tolerating a genuine bad patch without excusing a payer who
        ///   has plainly stopped paying.
        /// </summary>
        public const int MissedDaysGraceThreshold = 3;

        /// <summary>Clamps a proposed daily amount into [<see cref="MinAmountPerDay" />, <see cref="MaxAmountPerDay" />].</summary>
        public static int ClampAmountPerDay(int amountPerDay)
        {
            if (amountPerDay < MinAmountPerDay) return MinAmountPerDay;
            if (amountPerDay > MaxAmountPerDay) return MaxAmountPerDay;

            return amountPerDay;
        }

        /// <summary>
        ///   True when the payer's CURRENT gold covers today's single day of tribute. Unlike StipendPolicy.CanAfford
        ///   (the whole 30-day tranche, checked once up front), this is a PAY-AS-YOU-GO check: it is read fresh
        ///   every daily tick against the payer's live gold, never funded or reserved in advance, so it may say
        ///   yes one day and no the next as the payer's own fortunes change.
        /// </summary>
        public static bool CanPay(int payerGold, int amountPerDay) => payerGold >= amountPerDay;
    }
}
