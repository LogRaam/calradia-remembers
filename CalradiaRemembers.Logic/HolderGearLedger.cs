// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: the serializable top-level ledger the host persists (byte[] JSON,
// save-safe, mirrors NemesisRoster) so every holder's accumulated stock survives across saves. Plain
// carrier, no behaviour.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Serializable ledger of every holder's accumulated gear stock.</summary>
    public sealed class HolderGearLedger
    {
        /// <summary>One entry per holder Hero who currently keeps player gear taken across one or more escapes.</summary>
        public List<HolderGearStock> Holders { get; set; } = new List<HolderGearStock>();

        /// <summary>One-shot backfill/migration stamp (reserved; 0 = fresh), mirrors NemesisRoster.MigrationVersion.</summary>
        public int MigrationVersion { get; set; }
    }
}
