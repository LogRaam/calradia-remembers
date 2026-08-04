// Code written by Gabriel Mailhot, 04/08/2026.
// Documents tribute's pure policy: the daily-amount clamp, the fixed 30-day term, and the pay-as-you-go afford
// check read fresh every tick. The stake across every test here: a tribute that outpaces the payer's own purse
// must degrade gracefully day by day (CanPay says no, the behavior tolerates it up to the grace threshold), it
// must never be able to clamp the payer's gold negative, and the amount itself must never reach the lift
// unclamped, however the LLM proposed it (COUNCIL_ACTIONS.md's Kimi review, "tribute").
#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class TributePolicyTests
    {
        // The floor: a token amount (or a zero/negative amount from a malformed proposal) must never reach the
        // lift as-is, so an enemy leader is never bound to a tribute too small to be a real submission.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(49)]
        [TestCase(-100)]
        public void GIVEN_an_amount_below_the_floor_WHEN_clamped_THEN_it_becomes_the_minimum(int amount)
        {
            TributePolicy.ClampAmountPerDay(amount).Should().Be(TributePolicy.MinAmountPerDay);
        }

        // The ceiling: COUNCIL_ACTIONS.md's own Kimi review names 2000/day as the outer bound, so an over-generous
        // LLM proposal must not silently bind a payer to more than that, however rich the world implies they are.
        [TestCase(2001)]
        [TestCase(1_000_000)]
        public void GIVEN_an_amount_above_the_ceiling_WHEN_clamped_THEN_it_becomes_the_maximum(int amount)
        {
            TributePolicy.ClampAmountPerDay(amount).Should().Be(TributePolicy.MaxAmountPerDay);
        }

        // Boundary values pass through untouched: the clamp must not shift a value already inside its own range.
        [TestCase(50)]
        [TestCase(500)]
        [TestCase(2000)]
        public void GIVEN_an_amount_within_range_WHEN_clamped_THEN_it_is_unchanged(int amount)
        {
            TributePolicy.ClampAmountPerDay(amount).Should().Be(amount);
        }

        // Unlike grant_stipend's whole-tranche escrow, a tribute is PAY-AS-YOU-GO: the payer must cover only
        // TODAY'S single day, never the full term at once, so this reads exactly like an ordinary solvency check.
        [Test]
        public void GIVEN_the_payers_gold_WHEN_asked_if_todays_tribute_is_payable_THEN_it_only_needs_to_cover_one_day()
        {
            TributePolicy.CanPay(100, 100).Should().BeTrue();
            TributePolicy.CanPay(99, 100).Should().BeFalse();
            TributePolicy.CanPay(1_000_000, 2000).Should().BeTrue();
        }

        // A payer with zero or negative gold (a malformed read) can never pay, the floor CanPay must refuse
        // rather than silently accept: the STAKE is that CouncilTributeBehavior.Advance never clamps a payer's
        // gold negative, and this is the gate that keeps it from ever trying.
        [Test]
        public void GIVEN_a_payer_with_no_gold_WHEN_asked_if_tribute_is_payable_THEN_it_is_refused()
        {
            TributePolicy.CanPay(0, 50).Should().BeFalse();
            TributePolicy.CanPay(-10, 50).Should().BeFalse();
        }

        // Author's simplification, documented: a FIXED term (mirroring StipendPolicy.TrancheDays), never a second
        // LLM-chosen parameter alongside the amount.
        [Test]
        public void GIVEN_the_tribute_term_WHEN_asked_THEN_it_is_a_fixed_thirty_days()
        {
            TributePolicy.TributeDays.Should().Be(30);
        }

        // COUNCIL_ACTIONS.md's own Kimi review names this figure directly ("3 ticks manques = rupture"): pinned
        // here so a future tuning pass cannot silently drift it without a test noticing.
        [Test]
        public void GIVEN_the_grace_threshold_WHEN_asked_THEN_it_is_three_consecutive_missed_days()
        {
            TributePolicy.MissedDaysGraceThreshold.Should().Be(3);
        }
    }
}
