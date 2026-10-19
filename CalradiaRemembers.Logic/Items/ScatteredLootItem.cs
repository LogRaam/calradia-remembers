// Code written by Gabriel Mailhot, 24/07/2026.
// Scattered-loot recovery (ratified 2026-07-24, from AUDIT-NEMESIS M1): when the player FLEES a bandit
// captivity, the gear taken is no longer handed to a single minted nemesis holder (which resurrected a boss
// during his own quiet years and inflated his counters). Instead each item is scattered among the bandit
// bands of the CAPTURING culture, and the player recovers it piece by piece by defeating those bands. This is
// one such scattered piece: the persisted item (reusing HeldItem, so the strip/grant conversion is the same
// as the holder model), the CULTURE it was scattered into, and the day it was lost (recovery favours the
// oldest loss first, so a long-scattered stock drains in the order it was taken).

namespace CalradiaRemembers.Logic.Items
{
    /// <summary>One item taken from the player on a fled bandit escape, scattered among that culture's bands until recovered.</summary>
    public sealed class ScatteredLootItem
    {
        /// <summary>The item itself, in the same persisted form the holder ledger uses.</summary>
        public HeldItem Item { get; set; }

        /// <summary>The bandit-clan culture key of the capturing band (e.g. "looters"), matched against a defeated band on recovery.</summary>
        public string CultureKey { get; set; }

        /// <summary>The in-game day the item was lost, so recovery can drain the oldest losses first.</summary>
        public int LostDay { get; set; }
    }
}
