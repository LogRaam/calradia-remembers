// Code written by Gabriel Mailhot, 08/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerTradePolicyTests
    {
        [Test]
        public void GIVEN_a_price_within_the_honest_band_WHEN_clamping_THEN_the_negotiated_figure_stands()
        {
            PrisonerTradePolicy.ClampPrice(800, ransomAnchor: 1000).Should().Be(800);
        }

        [Test]
        public void GIVEN_a_lowball_below_the_floor_WHEN_clamping_THEN_it_rises_to_the_floor()
        {
            // Floor = 40% of the 1000 anchor.
            PrisonerTradePolicy.ClampPrice(10, ransomAnchor: 1000).Should().Be(400);
        }

        [Test]
        public void GIVEN_an_absurd_premium_WHEN_clamping_THEN_it_falls_to_the_ceiling()
        {
            // Ceiling = 3x the 1000 anchor.
            PrisonerTradePolicy.ClampPrice(50000, ransomAnchor: 1000).Should().Be(3000);
        }

        [Test]
        public void GIVEN_no_price_emitted_WHEN_clamping_THEN_the_sale_lands_at_the_floor_not_zero()
        {
            PrisonerTradePolicy.ClampPrice(0, ransomAnchor: 1000).Should().Be(400);
        }

        [Test]
        public void GIVEN_an_unreadable_ransom_anchor_WHEN_clamping_THEN_the_fallback_anchor_rules()
        {
            // Fallback anchor 500: floor 200, ceiling 1500.
            PrisonerTradePolicy.ClampPrice(9999, ransomAnchor: 0).Should().Be(1500);
            PrisonerTradePolicy.ClampPrice(100, ransomAnchor: -5).Should().Be(200);
        }

        [Test]
        public void GIVEN_a_worthless_captive_WHEN_clamping_THEN_the_absolute_minimum_price_holds()
        {
            // Anchor 60: raw floor would be 24, held up to the MinPrice of 50.
            PrisonerTradePolicy.ClampPrice(1, ransomAnchor: 60).Should().Be(PrisonerTradePolicy.MinPrice);
        }
    }
}
