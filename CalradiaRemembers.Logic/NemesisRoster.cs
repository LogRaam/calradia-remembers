// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar (increment 1): the serializable roster the host persists (byte[] JSON, save-safe) so the
// latent nemesis identities survive across saves. Plain carrier, no behaviour.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Serializable roster of nemesis identities, living and dead (a dead one is kept for the record but never recurs).</summary>
    public sealed class NemesisRoster
    {
        /// <summary>Every nemesis ever minted on this campaign.</summary>
        public List<NemesisIdentity> Nemeses { get; set; } = new List<NemesisIdentity>();

        /// <summary>One-shot backfill/migration stamp (reserved; 0 = fresh).</summary>
        public int MigrationVersion { get; set; }
    }
}
