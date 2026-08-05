// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 1 (COUNCIL_ACTIONS.md's lend_troops, Gabriel's own CONDITIONS): pure
// policy for HOW MANY troops, split across low/medium/high tiers, a lord parts with when the council lifts a
// lend_troops resolution. Gabriel's CONDITIONS deliberately OVERRIDE the existing 1:1 lend_troops policy
// (CourtActionPolicy.TroopLoanCount/MoveLowestTierTroops, which caps at 10 troops, gates on relation >= 20, and
// always takes the LOWEST tier first): (C) "jamais plus de 25% du party du Lord. Doit avoir une distribution de
// tiers avec faible, moyen, haut" - never more than a quarter of the lord's own healthy non-hero troop count, and
// the loan MUST carry a tier mix (never a pure levy dump) whenever the lord's own party actually fields higher
// tiers. The Kimi review (COUNCIL_ACTIONS.md) proposes exactly this shape: "cap 25% de l'effectif sain du lord,
// selection proportionnelle aux tiers presents (tirer au prorata des effectifs par tier, arrondi plancher, minimum
// 1 du tier dominant pour eviter un pret 100% levees)". (A) the DELIVERY mechanic (a temporary delegation party,
// never the lord himself, see COUNCIL_ACTIONS.md's Kimi review under lend_troops) and (B) the gift being PERMANENT
// both live in the mod (CalradiaRemembers.Assemblies.CouncilTroopDeliveryBehavior); this class only decides the
// COUNT per tier, engine-free so the tuning stays protected by tests.

using System;

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>
    ///   A party's healthy, non-hero troop count bucketed into three tiers. The mod maps the engine's own
    ///   CharacterObject.Tier into these buckets (see CalradiaRemembers.GameState.TroopTierBucketer: low = tier
    ///   1-2, medium = tier 3-4, high = tier 5+, a design call documented there); this struct only carries the
    ///   already-bucketed counts, so the split math below stays engine-free. Never negative (clamped at
    ///   construction) - a caller passing a bad count degrades to zero rather than corrupting the split.
    /// </summary>
    public readonly struct TierTroopCounts
    {
        public TierTroopCounts(int low, int medium, int high)
        {
            Low = Math.Max(0, low);
            Medium = Math.Max(0, medium);
            High = Math.Max(0, high);
        }

        public int Low { get; }
        public int Medium { get; }
        public int High { get; }
        public int Total => Low + Medium + High;
    }

    /// <summary>
    ///   Pure math for lend_troops at council (Gabriel's CONDITIONS, COUNCIL_ACTIONS.md): the tier-split loan a
    ///   lord's party would part with. Distinct from (and, for the council table, superseding) the 1:1 verb's own
    ///   CourtActionPolicy.TroopLoanCount/MoveLowestTierTroops.
    /// </summary>
    public static class CouncilTroopLoanPolicy
    {
        /// <summary>Gabriel's CONDITIONS (C): "jamais plus de 25% du party du Lord" - the hard ceiling, however healthy the party.</summary>
        public const float MaxLoanFraction = 0.25f;

        /// <summary>
        ///   The tier-split loan for a party fielding <paramref name="party" /> healthy troops: floor(total * 25%)
        ///   overall, distributed proportionally to the tiers actually present (largest-remainder rounding, so the
        ///   cap is used as fully as the roster allows), then corrected so the loan is never ALL low tier when the
        ///   party fields higher tiers too (Gabriel's "distribution avec faible/moyen/haut"; Kimi review: "jamais
        ///   un pret 100% levees"), and always carries at least one of the party's own DOMINANT tier (its most
        ///   numerous). Zero across the board for an empty or too-small party - the 25% cap floors to 0 below 4
        ///   total troops, so a tiny escort or lone-rider party has nothing spare to lend at all.
        /// </summary>
        public static TierTroopCounts ComputeLoan(TierTroopCounts party)
        {
            int total = party.Total;
            if (total <= 0) return new TierTroopCounts(0, 0, 0);

            var cap = (int) Math.Floor(total * MaxLoanFraction);
            if (cap <= 0) return new TierTroopCounts(0, 0, 0);

            int[] counts = {party.Low, party.Medium, party.High};
            var shares = new int[3];
            var fractions = new double[3];

            for (var i = 0; i < 3; i++)
            {
                double exact = cap * (double) counts[i] / total;
                shares[i] = (int) Math.Floor(exact);
                fractions[i] = exact - shares[i];
            }

            int remainder = cap - (shares[0] + shares[1] + shares[2]);

            // Largest-remainder method: the tier(s) whose floor lost the most gets the leftover unit(s) first, so
            // the cap is spent as fully as the roster allows rather than left idle by rounding. Ties favour the
            // HIGHER tier (index order Low/Medium/High), a small nudge toward mixing rather than pure levies.
            while (remainder > 0)
            {
                var best = -1;
                for (var i = 0; i < 3; i++)
                {
                    if (shares[i] >= counts[i]) continue; // no more of this tier left to lend

                    if (best == -1 || fractions[i] > fractions[best] || fractions[i] == fractions[best] && i > best)
                        best = i;
                }

                if (best == -1) break; // every tier already exhausted (cannot happen: sum(counts) >= cap)

                shares[best]++;
                fractions[best] = -1; // consumed - never picked twice in the same remainder pass
                remainder--;
            }

            // Gabriel's "never a pure levy dump": if the party fields medium/high troops at all, the loan must
            // show at least one of them rather than coming out entirely from the low tier.
            if (shares[1] == 0 && shares[2] == 0 && (counts[1] > 0 || counts[2] > 0) && shares[0] > 0)
            {
                shares[0]--;
                if (counts[2] > 0) shares[2] = 1;
                else shares[1] = 1;
            }

            // "at least one of the dominant tier": the party's own most-numerous tier must be represented once the
            // loan carries any troops at all, so a lord whose party is mostly elite riders never lends a token
            // handful of levies while the loan quietly skips his real strength.
            int dominant = counts[0] >= counts[1] && counts[0] >= counts[2]
                ? 0
                : counts[1] >= counts[2]
                    ? 1
                    : 2;

            if (shares[dominant] == 0 && counts[dominant] > 0)
            {
                var donor = -1;
                for (var i = 0; i < 3; i++)
                    if (i != dominant && shares[i] > 0 && (donor == -1 || shares[i] > shares[donor]))
                        donor = i;

                if (donor != -1)
                {
                    shares[donor]--;
                    shares[dominant]++;
                }
            }

            return new TierTroopCounts(shares[0], shares[1], shares[2]);
        }
    }
}
