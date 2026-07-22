// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: the per-holder persisted carrier (mirrors NemesisIdentity inside
// NemesisRoster). Keyed by the holder's Hero StringId rather than by nemesis identity, so ONE ledger shape
// covers both inheritance paths ratified for the FLED outcome: a bandit escape's gear goes to the region's
// nemesis's Hero, a lord's captivity keeps it on the lord's own Hero. No behaviour here, see
// CaptiveGearStockPolicy for the pure best-per-slot decision the engine applies on top of Items.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Everything a single holder Hero has accumulated from the player's escapes, additive across
    /// however many times the player has fled that holder's hold.</summary>
    public sealed class HolderGearStock
    {
        /// <summary>The holder's Hero StringId (a nemesis's minted Hero, or a lord, indifferently).</summary>
        public string HolderHeroStringId { get; set; }

        /// <summary>Every item ever taken from the player into this holder's keeping, worn or not.</summary>
        public List<HeldItem> Items { get; set; } = new List<HeldItem>();
    }
}
