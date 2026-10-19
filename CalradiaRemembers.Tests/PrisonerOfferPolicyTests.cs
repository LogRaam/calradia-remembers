// Code written by Gabriel Mailhot, 25/08/2026.
// PrisonerOffer letters name a price for a captive the player holds, and the read window's Accept settles the
// ransom at that figure. This policy is the ONLY place that figure is decided, so these tests pin it: a lord with
// a personal stake opens higher than an opportunist, and every figure stays inside the honest ransom band the
// face-to-face trade uses (PrisonerTradePolicy). If the premium escaped the band, a letter could name an absurd
// sum the Accept would then pay out; if it never rose, a kinsman would offer no more than a stranger.

#region

using CalradiaRemembers.Logic.Prisoners;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerOfferPolicyTests
    {
        // A lord buying back their own kin or vassal is moved to pay more than a mere opportunist; the whole point
        // of the personal-stake premium is that it shows in the figure the letter names.
        [Test]
        public void GIVEN_a_personal_stake_WHEN_pricing_the_offer_THEN_it_exceeds_an_opportunist_at_the_same_anchor()
        {
            PrisonerOfferPolicy.OfferAmount(1000, true)
                .Should().BeGreaterThan(PrisonerOfferPolicy.OfferAmount(1000, false));
        }

        // The offer must never escape the honest band ClampPrice enforces, or a letter could promise (and the
        // Accept then pay) a figure the face-to-face trade would never allow. A huge anchor's premium is capped
        // at the ceiling; a tiny anchor never drops below the floor.
        [Test]
        public void GIVEN_extreme_anchors_WHEN_pricing_THEN_the_offer_stays_inside_the_ransom_band()
        {
            int bigAnchor = 100000;
            PrisonerOfferPolicy.OfferAmount(bigAnchor, true)
                .Should().BeLessThanOrEqualTo(PrisonerTradePolicy.ClampPrice(int.MaxValue, bigAnchor)); // the ceiling
            PrisonerOfferPolicy.OfferAmount(1, true)
                .Should().BeGreaterThanOrEqualTo(PrisonerTradePolicy.MinPrice); // never a giveaway
        }

        // fkasad (Nexus, 12/10/2026): "if they really want their guy back, they could at least raise the price a bit." A
        // lord whose first envoy was sent away sends the next with a better offer.
        [Test]
        public void GIVEN_an_envoy_already_turned_away_WHEN_the_lord_sends_another_THEN_he_offers_more()
        {
            PrisonerOfferPolicy.OfferAmount(1000, true, envoysTurnedAway: 1)
                .Should().BeGreaterThan(PrisonerOfferPolicy.OfferAmount(1000, true));
        }

        // The raise stays inside the honest band like every other figure: a refusal is never a lever to an absurd sum.
        [Test]
        public void GIVEN_many_envoys_turned_away_WHEN_pricing_THEN_the_raise_stays_inside_the_band()
        {
            PrisonerOfferPolicy.OfferAmount(1000, true, envoysTurnedAway: 50)
                .Should().BeLessThanOrEqualTo(PrisonerTradePolicy.MaxPrice(1000));
        }

        // A missing / unreadable ransom value must not crash or price at zero: it falls to the policy's fallback
        // anchor, the same one PrisonerTradePolicy uses, so the letter still names a fair figure.
        [Test]
        public void GIVEN_a_nonpositive_anchor_WHEN_pricing_THEN_it_falls_back_rather_than_zero()
        {
            PrisonerOfferPolicy.OfferAmount(0, false)
                .Should().Be(PrisonerTradePolicy.ClampPrice(PrisonerTradePolicy.FallbackAnchor, PrisonerTradePolicy.FallbackAnchor));
        }
    }
}
