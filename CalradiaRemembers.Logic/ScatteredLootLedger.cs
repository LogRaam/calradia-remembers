// Code written by Gabriel Mailhot, 24/07/2026.
// The persisted list of the player's scattered captivity loot (see ScatteredLootItem). Serialized as a byte[]
// JSON blob by ScatteredLootBehavior on its own additive save key, degrading to empty on an absent key or a
// corrupt blob, exactly like NemesisRoster / HolderGearLedger. MigrationVersion is reserved for a future
// change to the scatter shape; unused at v1.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Save-serializable carrier for every item currently scattered among bandit bands, awaiting recovery.</summary>
    public sealed class ScatteredLootLedger
    {
        /// <summary>Every scattered item still out there. Empty on a fresh or never-triggered campaign.</summary>
        public List<ScatteredLootItem> Items { get; set; } = new List<ScatteredLootItem>();

        /// <summary>Reserved for a future scatter-shape migration. 0 at v1.</summary>
        public int MigrationVersion { get; set; }
    }
}
