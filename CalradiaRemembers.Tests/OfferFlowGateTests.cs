// Code written by Gabriel Mailhot, 11/07/2026.
// Covers the pure routing decision behind the offer ("give") button (OfferFlowGate), which the mod's
// OfferController composes. OfferController itself and its picker/inquiry/bridge glue live in the net472 mod
// project (engine-bound: ItemPickerService, Verbs.BuyPrisonerVerb, NpcMemoryCampaignBehavior, MBInformationManager,
// RunLlmExchangeAsync), which this net10 test project cannot reference, so that glue is exercised in game rather
// than here. Only the precedence table is asserted below.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OfferFlowGateTests
    {
        [Test]
        public void GIVEN_an_outstanding_bargain_WHEN_routing_THEN_it_settles_the_bargain()
        {
            OfferFlowGate.Route(true, false, 0).Should().Be(OfferRoute.ItemBargain);
        }

        [Test]
        public void GIVEN_an_outstanding_bargain_alongside_a_supply_and_captives_WHEN_routing_THEN_the_bargain_still_wins()
        {
            OfferFlowGate.Route(true, true, 3).Should().Be(OfferRoute.ItemBargain);
        }

        [Test]
        public void GIVEN_no_bargain_but_an_outstanding_supply_WHEN_routing_THEN_it_settles_the_supply()
        {
            OfferFlowGate.Route(false, true, 0).Should().Be(OfferRoute.SupplyQuest);
        }

        [Test]
        public void GIVEN_a_supply_alongside_held_captives_but_no_bargain_WHEN_routing_THEN_the_supply_wins_over_the_captives()
        {
            OfferFlowGate.Route(false, true, 5).Should().Be(OfferRoute.SupplyQuest);
        }

        [Test]
        public void GIVEN_no_bargain_or_supply_but_held_captives_WHEN_routing_THEN_the_category_choice_is_offered()
        {
            OfferFlowGate.Route(false, false, 1).Should().Be(OfferRoute.CategoryChoice);
        }

        [Test]
        public void GIVEN_no_bargain_or_supply_and_no_captives_WHEN_routing_THEN_the_inventory_picker_opens_directly()
        {
            OfferFlowGate.Route(false, false, 0).Should().Be(OfferRoute.InventoryDirect);
        }

        [Test]
        public void GIVEN_a_negative_prisoner_count_WHEN_routing_with_nothing_outstanding_THEN_it_still_goes_straight_to_the_picker()
        {
            // Defensive: a count of zero or below means no captives to offer, so the plain picker opens.
            OfferFlowGate.Route(false, false, -1).Should().Be(OfferRoute.InventoryDirect);
        }
    }
}
