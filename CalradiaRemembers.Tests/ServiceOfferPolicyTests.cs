// Code written by Gabriel Mailhot, 25/08/2026.
// A ServiceOffer letter dangles ONE enticement (gold / a castle / troops) chosen by the poaching lord's means.
// This pure choice is the only place that decision lives, and the letter's prose is built from it, so these tests
// pin the priority: a lord with a fief to spare promises a castle; one who leads men promises soldiers; a lord
// with neither falls back to gold. If the priority were wrong, a landless lord could "grant a castle" they do not
// have, or a wealthy ruler would never dangle the fief that would actually tempt a landless player.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ServiceOfferPolicyTests
    {
        // A castle is the strongest lure and takes precedence whenever the lord actually has a fief to spare, even
        // if they also lead men: the offer should reach for the biggest thing the lord can truly promise.
        [Test]
        public void GIVEN_a_lord_with_a_spare_fief_WHEN_choosing_THEN_a_castle_is_offered_even_if_they_also_lead_troops()
        {
            ServiceOfferPolicy.ChooseEnticement(canOfferCastle: true, canOfferTroops: true)
                .Should().Be(ServiceEnticement.Castle);
        }

        // No fief to grant, but men in the field: soldiers are the next real thing on offer.
        [Test]
        public void GIVEN_no_fief_but_a_field_party_WHEN_choosing_THEN_troops_are_offered()
        {
            ServiceOfferPolicy.ChooseEnticement(canOfferCastle: false, canOfferTroops: true)
                .Should().Be(ServiceEnticement.Troops);
        }

        // Neither a spare fief nor a party: the fallback is gold, so a lord always has SOMETHING concrete to dangle.
        [Test]
        public void GIVEN_neither_a_fief_nor_a_party_WHEN_choosing_THEN_gold_is_the_fallback()
        {
            ServiceOfferPolicy.ChooseEnticement(canOfferCastle: false, canOfferTroops: false)
                .Should().Be(ServiceEnticement.Gold);
        }

        // The accept warm-up must be a real, positive nudge (a light hook still counts for something), but the
        // policy's own doc frames it as light; this simply pins that accepting is never a no-op.
        [Test]
        public void GIVEN_the_accept_warmup_WHEN_read_THEN_it_is_a_small_positive_nudge()
        {
            ServiceOfferPolicy.AcceptRelationWarmup.Should().BeGreaterThan(0);
            ServiceOfferPolicy.AcceptRelationWarmup.Should().BeLessThanOrEqualTo(10);
        }
    }
}
