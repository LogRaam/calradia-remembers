// Code written by Gabriel Mailhot, 04/08/2026.
// CouncilTroopLoanPolicy decides HOW MANY troops, split by tier, a lord's party parts with for a council lend_troops
// resolution. Gabriel's own written CONDITIONS (COUNCIL_ACTIONS.md) are authoritative and are what these tests
// pin: (C) "jamais plus de 25% du party du Lord. Doit avoir une distribution de tiers avec faible, moyen, haut" -
// a hard 25% ceiling AND a genuine tier mix, never a pure levy dump, whenever the party actually fields higher
// tiers. Get the cap wrong and a council pledge could gut a lord's real fighting strength (or a battle-deciding
// windfall for the player); get the mix wrong and the promise reads as "troops" while every soldier delivered is
// an unarmoured levy, the exact LLM-says/game-does gap this whole project exists to close.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class CouncilTroopLoanPolicyTests
    {
        // The headline stake (Gabriel's condition C): however large or lopsided the party, the loan must never
        // exceed a quarter of its total healthy troops. Swept across a range of sizes so no single case hides a
        // rounding bug that would let a council pledge bleed a lord's party white.
        [TestCase(4)]
        [TestCase(10)]
        [TestCase(37)]
        [TestCase(100)]
        [TestCase(200)]
        [TestCase(999)]
        public void GIVEN_a_pure_low_tier_party_of_various_sizes_WHEN_the_loan_is_computed_THEN_it_never_exceeds_25_percent(int size)
        {
            var party = new TierTroopCounts(size, 0, 0);

            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

            loan.Total.Should().BeLessThanOrEqualTo((int) (size * CouncilTroopLoanPolicy.MaxLoanFraction));
        }

        // The floor side of the same stake: a tiny party (under 4 troops) has 25% floor to zero, so it must lend
        // NOTHING at all rather than round up to a token soldier that would still be a real loss to a lone rider
        // or a skeleton escort.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GIVEN_a_party_too_small_to_spare_a_quarter_WHEN_the_loan_is_computed_THEN_it_lends_nothing(int size)
        {
            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(new TierTroopCounts(size, 0, 0));

            loan.Total.Should().Be(0);
            loan.Low.Should().Be(0);
            loan.Medium.Should().Be(0);
            loan.High.Should().Be(0);
        }

        // An entirely empty party (dead/routed since the roster was read) must degrade to zero, never throw or
        // divide by the party's own total.
        [Test]
        public void GIVEN_an_empty_party_WHEN_the_loan_is_computed_THEN_it_lends_nothing()
        {
            CouncilTroopLoanPolicy.ComputeLoan(new TierTroopCounts(0, 0, 0)).Total.Should().Be(0);
        }

        // Gabriel's condition C, the mix half: a party that fields medium troops too must not have them silently
        // rounded away by the proportional split (100 low vs 1 medium: 1/101 of a 25-troop cap floors to zero) -
        // the "never a pure levy dump" correction must still surface at least one of the higher tier.
        [Test]
        public void GIVEN_a_mostly_low_tier_party_with_a_thin_sliver_of_medium_WHEN_the_loan_is_computed_THEN_at_least_one_medium_troop_is_included()
        {
            var party = new TierTroopCounts(100, 1, 0);

            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

            loan.Medium.Should().BeGreaterThanOrEqualTo(1);
            loan.Total.Should().BeLessThanOrEqualTo(25);
        }

        // Same stake, the high-tier variant: a lord fielding a handful of elite riders among many levies must
        // still see at least one high-tier troop in the loan, favouring the HIGHEST tier present over medium when
        // both exist (the "never a pure levy dump" fix prefers High).
        [Test]
        public void GIVEN_a_mostly_low_tier_party_with_a_thin_sliver_of_high_tier_WHEN_the_loan_is_computed_THEN_at_least_one_high_tier_troop_is_included()
        {
            var party = new TierTroopCounts(96, 2, 2);

            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

            loan.High.Should().BeGreaterThanOrEqualTo(1);
        }

        // A party with real depth in all three tiers should see a genuinely proportional split, not a cap dumped
        // onto one tier: this pins the largest-remainder distribution directly against hand-worked expectations.
        [Test]
        public void GIVEN_a_party_with_a_clean_proportional_mix_WHEN_the_loan_is_computed_THEN_the_split_matches_the_proportions()
        {
            // total 100, cap 25: 80 low / 15 medium / 5 high -> exact shares 20 / 3.75 / 1.25, remainder 1 goes
            // to medium (largest fractional remainder, .75 > .25).
            var party = new TierTroopCounts(80, 15, 5);

            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

            loan.Low.Should().Be(20);
            loan.Medium.Should().Be(4);
            loan.High.Should().Be(1);
            loan.Total.Should().Be(25);
        }

        // An evenly-split party (equal counts in every tier) must come back with an even loan, and the dominant
        // tier tie-break (Low chosen first when all three are equal) must never leave any tier at zero when the
        // cap can afford one of each.
        [Test]
        public void GIVEN_a_party_split_evenly_across_all_three_tiers_WHEN_the_loan_is_computed_THEN_the_loan_is_also_even()
        {
            var party = new TierTroopCounts(4, 4, 4);

            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

            loan.Low.Should().Be(1);
            loan.Medium.Should().Be(1);
            loan.High.Should().Be(1);
        }

        // Gabriel's condition C, "au moins le tier dominant": a party overwhelmingly elite (dominant tier = High)
        // must not have the loan come back all-low from a rounding quirk - the dominant tier must always show.
        [Test]
        public void GIVEN_a_party_dominated_by_its_high_tier_WHEN_the_loan_is_computed_THEN_the_dominant_tier_is_represented()
        {
            var party = new TierTroopCounts(1, 1, 40);

            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

            loan.High.Should().BeGreaterThanOrEqualTo(1);
        }

        // A party with only ONE tier present has no mix to offer - the "never all levies" correction must not
        // fire when there is genuinely nothing else to draw from, and the loan must still respect the 25% cap.
        [Test]
        public void GIVEN_a_party_with_only_one_tier_present_WHEN_the_loan_is_computed_THEN_it_lends_only_from_that_tier_up_to_the_cap()
        {
            TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(new TierTroopCounts(0, 40, 0));

            loan.Low.Should().Be(0);
            loan.Medium.Should().Be(10);
            loan.High.Should().Be(0);
        }

        // A negative bucket (a caller's own bug, or a corrupt read) must clamp to zero at construction rather
        // than corrupting the split with a negative total or a negative share.
        [Test]
        public void GIVEN_a_bucket_constructed_with_a_negative_count_WHEN_read_back_THEN_it_clamps_to_zero()
        {
            var counts = new TierTroopCounts(-5, 10, -1);

            counts.Low.Should().Be(0);
            counts.Medium.Should().Be(10);
            counts.High.Should().Be(0);
            counts.Total.Should().Be(10);
        }

        // Invariant sweep: across a broad spread of party shapes, the loan must NEVER exceed the party's own
        // count in any single tier (the policy can only redistribute what actually exists), and the grand total
        // must never exceed 25% of the party's own total, whatever the tier mix. A single hand-worked example
        // cannot catch every rounding edge; this walks a grid of shapes so a future tuning change cannot quietly
        // break either invariant without a test noticing.
        [Test]
        public void GIVEN_a_wide_spread_of_party_shapes_WHEN_the_loan_is_computed_THEN_it_never_exceeds_the_cap_or_any_tiers_own_count()
        {
            int[] sizes = {0, 1, 2, 3, 4, 5, 7, 11, 23, 50, 77, 123};

            foreach (int low in sizes)
            foreach (int medium in sizes)
            foreach (int high in sizes)
            {
                var party = new TierTroopCounts(low, medium, high);
                TierTroopCounts loan = CouncilTroopLoanPolicy.ComputeLoan(party);

                loan.Low.Should().BeLessThanOrEqualTo(party.Low);
                loan.Medium.Should().BeLessThanOrEqualTo(party.Medium);
                loan.High.Should().BeLessThanOrEqualTo(party.High);
                loan.Total.Should().BeLessThanOrEqualTo((int) (party.Total * CouncilTroopLoanPolicy.MaxLoanFraction));
            }
        }
    }
}
