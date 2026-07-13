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
        // The bag exists to settle a pending bargain directly (SettleItemBargain); if this loses top
        // precedence, the player lands on the wrong picker and the bargain never actually gets paid off.
        [Test]
        public void GIVEN_an_outstanding_bargain_WHEN_routing_THEN_it_settles_the_bargain()
        {
            OfferFlowGate.Route(true, false, 0).Should().Be(OfferRoute.ItemBargain);
        }

        // Pins the precedence order itself: a bargain must win even when a supply quest and captives are
        // also on the table, so a later reordering of the checks can never bury it behind either.
        [Test]
        public void GIVEN_an_outstanding_bargain_alongside_a_supply_and_captives_WHEN_routing_THEN_the_bargain_still_wins()
        {
            OfferFlowGate.Route(true, true, 3).Should().Be(OfferRoute.ItemBargain);
        }

        // A ProvideGoods supply quest is only ever closed by a real hand-over through this control; miss
        // this route and there is no way to deliver the goods and complete the quest through the bag.
        [Test]
        public void GIVEN_no_bargain_but_an_outstanding_supply_WHEN_routing_THEN_it_settles_the_supply()
        {
            OfferFlowGate.Route(false, true, 0).Should().Be(OfferRoute.SupplyQuest);
        }

        // A player who both owes goods and holds prisoners must still be dropped straight into the supply
        // picker, not diverted into the goods-or-prisoner category choice that only applies when nothing
        // is already outstanding.
        [Test]
        public void GIVEN_a_supply_alongside_held_captives_but_no_bargain_WHEN_routing_THEN_the_supply_wins_over_the_captives()
        {
            OfferFlowGate.Route(false, true, 5).Should().Be(OfferRoute.SupplyQuest);
        }

        // This is the only route through which a held prisoner can be offered at all (OfferController's
        // CategoryChoice branch); skip it and there is no way to give a prisoner through the bag.
        [Test]
        public void GIVEN_no_bargain_or_supply_but_held_captives_WHEN_routing_THEN_the_category_choice_is_offered()
        {
            OfferFlowGate.Route(false, false, 1).Should().Be(OfferRoute.CategoryChoice);
        }

        // The common case (nothing owed, nothing held) must not force an unneeded category-choice popup
        // on a player who only wants to hand over an item or gold.
        [Test]
        public void GIVEN_no_bargain_or_supply_and_no_captives_WHEN_routing_THEN_the_inventory_picker_opens_directly()
        {
            OfferFlowGate.Route(false, false, 0).Should().Be(OfferRoute.InventoryDirect);
        }

        // Defensive floor: heldPrisonerCount should never be negative, but if the caller's count ever
        // misbehaves, this must still fail safe to the plain picker rather than mis-triggering CategoryChoice.
        [Test]
        public void GIVEN_a_negative_prisoner_count_WHEN_routing_with_nothing_outstanding_THEN_it_still_goes_straight_to_the_picker()
        {
            // Defensive: a count of zero or below means no captives to offer, so the plain picker opens.
            OfferFlowGate.Route(false, false, -1).Should().Be(OfferRoute.InventoryDirect);
        }
    }
}
