// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08): the LLM proposes a price in-fiction, but the BRIDGE IS LAW here.
// BuyPrisonerVerb and SellPrisonerVerb both trust PrisonerTradePolicy.ClampPrice as the one place that keeps
// that price honest around the game's own ransom valuation. A floor set too low turns a captive into a
// giveaway; a ceiling set too high lets a silver tongue talk a lord into an absurd premium; either failure
// undermines the bounty-hunting loop this trade exists to open.

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
        // A genuinely reasonable negotiation must pass through untouched: the clamp exists to catch abuse,
        // not to re-price every honest deal regardless of what was actually agreed.
        [Test]
        public void GIVEN_a_price_within_the_honest_band_WHEN_clamping_THEN_the_negotiated_figure_stands()
        {
            PrisonerTradePolicy.ClampPrice(800, ransomAnchor: 1000).Should().Be(800);
        }

        // The 40% floor stops a captive being haggled down to a giveaway; without it a player (or an
        // over-eager LLM) could talk a lord into selling a valuable hostage for pocket change.
        [Test]
        public void GIVEN_a_lowball_below_the_floor_WHEN_clamping_THEN_it_rises_to_the_floor()
        {
            // Floor = 40% of the 1000 anchor.
            PrisonerTradePolicy.ClampPrice(10, ransomAnchor: 1000).Should().Be(400);
        }

        // The 3x ceiling is the mirror guard: the LLM negotiates in-fiction, but it must never be able to
        // agree its way into paying (or demanding) an absurd sum for a single captive.
        [Test]
        public void GIVEN_an_absurd_premium_WHEN_clamping_THEN_it_falls_to_the_ceiling()
        {
            // Ceiling = 3x the 1000 anchor.
            PrisonerTradePolicy.ClampPrice(50000, ransomAnchor: 1000).Should().Be(3000);
        }

        // A missing price is a broken emission, not a broken promise: the exchange already promised a sale,
        // so it must land at the honest floor, never at zero or a free hand-over.
        [Test]
        public void GIVEN_no_price_emitted_WHEN_clamping_THEN_the_sale_lands_at_the_floor_not_zero()
        {
            PrisonerTradePolicy.ClampPrice(0, ransomAnchor: 1000).Should().Be(400);
        }

        // BuyPrisonerVerb.RansomAnchor falls back to FallbackAnchor whenever the game's own ransom model
        // cannot be read for a captive; a raw anchor of 0 would otherwise collapse floor and ceiling to
        // nothing and let any price through unclamped.
        [Test]
        public void GIVEN_an_unreadable_ransom_anchor_WHEN_clamping_THEN_the_fallback_anchor_rules()
        {
            // Fallback anchor 500: floor 200, ceiling 1500.
            PrisonerTradePolicy.ClampPrice(9999, ransomAnchor: 0).Should().Be(1500);
            PrisonerTradePolicy.ClampPrice(100, ransomAnchor: -5).Should().Be(200);
        }

        // The 40% floor alone is not enough for a very low-value captive (24 denars here); MinPrice is the
        // absolute backstop so no prisoner, however lowly, ever trades for a trivial sum.
        [Test]
        public void GIVEN_a_worthless_captive_WHEN_clamping_THEN_the_absolute_minimum_price_holds()
        {
            // Anchor 60: raw floor would be 24, held up to the MinPrice of 50.
            PrisonerTradePolicy.ClampPrice(1, ransomAnchor: 60).Should().Be(PrisonerTradePolicy.MinPrice);
        }
    }
}
