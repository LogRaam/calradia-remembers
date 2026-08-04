// Code written by Gabriel Mailhot, 03/08/2026.
// Documents grant_stipend's pure policy: the daily-amount clamp, the escrow total, and the afford gate both the
// offering (at the floor, before any amount is proposed) and the lift (at the real proposed amount) read. The
// stake across every test here is the same: a promise that outruns the player's purse must be refused before the
// lift ever touches the world, never applied partially and never left to fail silently mid-term (COUNCIL_ACTIONS.md
// "grant_stipend", ESCROW model - Kimi review, adopted as-is).

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class StipendPolicyTests
    {
        // The floor: a token amount (or a zero/negative amount from a malformed proposal) must never reach the
        // lift as-is, so a companion is never granted a stipend too small to be a real mechanical payoff.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(49)]
        [TestCase(-100)]
        public void GIVEN_an_amount_below_the_floor_WHEN_clamped_THEN_it_becomes_the_minimum(int amount)
        {
            StipendPolicy.ClampAmountPerDay(amount).Should().Be(StipendPolicy.MinAmountPerDay);
        }

        // The ceiling: an over-generous LLM proposal must not silently become a purse-breaking daily payment;
        // CanAfford is the OTHER half of this guard (the player's actual gold), this pins the amount itself.
        [TestCase(501)]
        [TestCase(5000)]
        public void GIVEN_an_amount_above_the_ceiling_WHEN_clamped_THEN_it_becomes_the_maximum(int amount)
        {
            StipendPolicy.ClampAmountPerDay(amount).Should().Be(StipendPolicy.MaxAmountPerDay);
        }

        // Boundary values pass through untouched: the clamp must not shift a value already inside its own range.
        [TestCase(50)]
        [TestCase(100)]
        [TestCase(500)]
        public void GIVEN_an_amount_within_range_WHEN_clamped_THEN_it_is_unchanged(int amount)
        {
            StipendPolicy.ClampAmountPerDay(amount).Should().Be(amount);
        }

        // The escrow the lift must deduct in one shot (author's design: funded up front so the promise can never
        // fail partway through its 30 days) is exactly the daily rate times the fixed tranche, nothing more.
        [Test]
        public void GIVEN_an_amount_per_day_WHEN_the_total_cost_is_asked_THEN_it_is_the_amount_times_the_tranche()
        {
            StipendPolicy.TotalCost(100).Should().Be(100 * StipendPolicy.TrancheDays);
            StipendPolicy.TotalCost(StipendPolicy.MinAmountPerDay).Should().Be(StipendPolicy.MinTrancheCost);
        }

        // The affordability gate both the offering (at the floor) and the lift (at the real amount) read: a purse
        // exactly equal to the tranche is enough, one denar short is refused, never half-funded.
        [Test]
        public void GIVEN_the_players_gold_WHEN_afford_is_asked_THEN_it_requires_covering_the_whole_tranche()
        {
            int cost = StipendPolicy.TotalCost(100);

            StipendPolicy.CanAfford(cost, 100).Should().BeTrue();
            StipendPolicy.CanAfford(cost - 1, 100).Should().BeFalse();
            StipendPolicy.CanAfford(cost + 500, 100).Should().BeTrue();
        }

        // MinTrancheCost is the floor the OFFERING checks before the LLM has proposed any amount (the actual
        // proposed amount is unknown at offer time, see ResolutionOfferingResolver.OfferedKindKeys): it must equal
        // the cheapest possible real tranche, or the offering could teach grant_stipend when even the floor is
        // unaffordable, or withhold it when the floor plainly is.
        [Test]
        public void GIVEN_the_minimum_tranche_cost_WHEN_computed_THEN_it_matches_the_floor_rate_over_the_full_tranche()
        {
            StipendPolicy.MinTrancheCost.Should().Be(StipendPolicy.MinAmountPerDay * StipendPolicy.TrancheDays);
        }
    }
}
