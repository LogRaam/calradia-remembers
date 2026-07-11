// Code written by Gabriel Mailhot, 11/07/2026.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Where the one "what do you offer?" control routes, given what is outstanding with the current NPC and
    ///   whether the player holds hero captives. Pure so it is unit-testable without the engine; the engine glue
    ///   (running the pickers/inquiries) lives in the mod's OfferController, which composes this.
    /// </summary>
    public enum OfferRoute
    {
        /// <summary>An item-barter bargain awaits goods: settle it directly through the bargain picker.</summary>
        ItemBargain,

        /// <summary>A ProvideGoods supply task awaits delivery: hand over the matching goods through the supply picker.</summary>
        SupplyQuest,

        /// <summary>The player holds hero captives: first ask whether they offer goods/gold or a prisoner.</summary>
        CategoryChoice,

        /// <summary>Nothing outstanding and no captives held: go straight to the item/gold picker.</summary>
        InventoryDirect
    }

    /// <summary>
    ///   The pure routing decision behind the offer ("give") button. Precedence, highest first: an outstanding
    ///   item bargain, then an outstanding supply quest, then (when the player holds hero captives) the
    ///   goods-or-prisoner category choice, else the plain inventory/gold picker.
    /// </summary>
    public static class OfferFlowGate
    {
        /// <summary>
        ///   Picks the offer route. Returns <see cref="OfferRoute.ItemBargain" /> when a bargain is outstanding
        ///   (it wins over everything); else <see cref="OfferRoute.SupplyQuest" /> when a supply quest is
        ///   outstanding; else <see cref="OfferRoute.CategoryChoice" /> when the player holds at least one hero
        ///   captive; else <see cref="OfferRoute.InventoryDirect" />.
        /// </summary>
        public static OfferRoute Route(bool hasOutstandingBargain, bool hasOutstandingSupply, int heldPrisonerCount)
        {
            if (hasOutstandingBargain) return OfferRoute.ItemBargain;
            if (hasOutstandingSupply) return OfferRoute.SupplyQuest;
            if (heldPrisonerCount > 0) return OfferRoute.CategoryChoice;

            return OfferRoute.InventoryDirect;
        }
    }
}
