// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module, Partie 1 (COUNCIL_ACTIONS.md's give_item, "cadeau d'objet... comme un pot de
// vin"): give_item's pure policy. A seated lord (ClanLords or WarCouncil council) gives the PLAYER a named item
// from THEIR OWN party's stores, the REVERSE of the existing 1:1 give_item action
// (BannerlordGameStateBridge.ExecuteGiveItem, player's party -> NPC). The one rule this policy pins: a quest item
// (one worth 0 denars, e.g. the Dragon Banner) is NEVER giveable, mirroring the existing filter
// CalradiaRemembers.Items.ItemPickerService.BuildEligibleItems already applies to the player's own party
// (item is not {Value: > 0}), the same discipline, now shared by BOTH directions of the give_item gesture.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pure policy for give_item: which items in a party's ItemRoster may ever be given away.</summary>
    public static class GiveItemPolicy
    {
        /// <summary>
        ///   True when an item of this value may be given away at all. A non-positive value marks a quest item
        ///   (the Dragon Banner and its kin carry Value == 0 in vanilla): those are never giveable, whichever
        ///   direction the gift moves, so a council gift can never accidentally hand over, or receive, a
        ///   scripted quest prop.
        /// </summary>
        public static bool IsGiveableItem(int itemValue) => itemValue > 0;
    }
}
