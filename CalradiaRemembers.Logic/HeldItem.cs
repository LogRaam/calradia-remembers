// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence (ratified 2026-07-2x): a single piece of the player's own equipment now
// held by a captor (a nemesis or a lord), persisted as a StringId + modifier rather than a live
// Equipment/ItemObject graph (this project has no game-engine reference, and a live object could not
// survive a save anyway). Tier and Value are copied at the moment of loss so the pure best-per-slot policy
// (CaptiveGearStockPolicy) never needs to resolve the real ItemObject to compare items.

namespace CalradiaRemembers.Logic
{
    /// <summary>A persisted record of one item taken from the player, enough to rebuild the real
    /// EquipmentElement later (wearing it on the holder, or granting it back to the player on recovery).</summary>
    public sealed class HeldItem
    {
        /// <summary>The ItemObject's StringId (e.g. "sword_arming_sword_t6").</summary>
        public string ItemStringId { get; set; }

        /// <summary>The ItemModifier's StringId (e.g. "sharp"), or null/empty when the item is unmodified.</summary>
        public string ModifierStringId { get; set; }

        /// <summary>Which equipped slot this item occupied on the player.</summary>
        public HeldEquipmentSlot Slot { get; set; }

        /// <summary>The item's ItemObject.Tier at the moment it was taken (the "most advantageous" policy's primary sort key).</summary>
        public int Tier { get; set; }

        /// <summary>The item's ItemObject.Value at the moment it was taken (the tie-break under equal tier).</summary>
        public int Value { get; set; }
    }
}
