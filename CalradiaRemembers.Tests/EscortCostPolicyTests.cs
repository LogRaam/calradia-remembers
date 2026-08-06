// Code written by Gabriel Mailhot, 06/08/2026.
// EscortCostPolicy prices the per-correspondent ARMED ESCORT: a one-off fee that makes a whole correspondence
// thread's couriers immune to bandit interception. It exists for the player report (2026-08-05) that letters to a
// far correspondent were lost almost every time; the escort is the premium guarantee of delivery over that long
// road, and its price is what these tests pin. The mod charges exactly what EscortPremium returns and, on payment,
// sets NpcProfile.EscortedChannel, so a wrong price here is real coin wrongly taken (or the feature made trivially
// cheap). The distance comes from the engine's MapDistanceModel, so the clamps also guard the price against an
// unexpectedly large or malformed distance value.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class EscortCostPolicyTests
    {
        // The escort guards armed men, not one lone rider, so the shortest possible escorted road must still cost
        // more than the plain 500-denar courier hire: a zero (next-door) distance falls back to the base premium,
        // never something cheaper or free, which would make the guarantee worthless to price in.
        [Test]
        public void GIVEN_a_zero_distance_WHEN_pricing_THEN_it_charges_the_base_premium()
        {
            EscortCostPolicy.EscortPremium(0f).Should().Be(EscortCostPolicy.BasePremium);
        }

        // The whole reason the feature exists (player report: far letters lost almost every time): a longer road
        // must cost MORE than a short one, so the premium genuinely scales with the distance rather than being a
        // flat fee that ignores the very case it was built for.
        [Test]
        public void GIVEN_a_longer_road_WHEN_pricing_THEN_it_costs_more_than_a_short_one()
        {
            int near = EscortCostPolicy.EscortPremium(100f);
            int far = EscortCostPolicy.EscortPremium(1000f);

            far.Should().BeGreaterThan(near);
        }

        // Pins the exact per-distance arithmetic (base + rate * distance) at a mid-range distance, so a silent
        // change to either constant that shifts what the player is charged is caught here rather than in-game.
        [Test]
        public void GIVEN_a_mid_range_distance_WHEN_pricing_THEN_it_is_the_base_plus_the_scaled_rate()
        {
            // 800 + round(400 * 1.5) = 800 + 600 = 1400.
            EscortCostPolicy.EscortPremium(400f).Should().Be(1400);
        }

        // The ceiling exists so a huge engine distance (the MapDistanceModel scale is TUNE-sensitive) can never
        // inflate the escort into an extortionate fee. Without it a cross-map thread could cost tens of thousands,
        // pricing the very players the report was about out of the fix.
        [Test]
        public void GIVEN_a_very_large_distance_WHEN_pricing_THEN_it_is_capped_at_the_maximum()
        {
            EscortCostPolicy.EscortPremium(1_000_000f).Should().Be(EscortCostPolicy.MaxPremium);
        }

        // Defensive: a malformed distance (negative or NaN, e.g. an engine pathing miss) must fail safe to the
        // base premium, never to something free or nonsensical that would let the player buy immunity for nothing.
        [TestCase(-50f)]
        [TestCase(float.NaN)]
        public void GIVEN_a_negative_or_NaN_distance_WHEN_pricing_THEN_it_falls_back_to_the_base_premium(float distance)
        {
            EscortCostPolicy.EscortPremium(distance).Should().Be(EscortCostPolicy.BasePremium);
        }

        // The result must always sit inside the advertised band, whatever the distance, so no code path ever
        // charges below the floor or above the ceiling: those bounds are the contract the mod and UI rely on.
        [TestCase(0f)]
        [TestCase(250f)]
        [TestCase(5000f)]
        [TestCase(50000f)]
        public void GIVEN_any_distance_WHEN_pricing_THEN_it_stays_within_the_clamp_band(float distance)
        {
            int premium = EscortCostPolicy.EscortPremium(distance);

            premium.Should().BeInRange(EscortCostPolicy.MinPremium, EscortCostPolicy.MaxPremium);
        }
    }
}
