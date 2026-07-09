// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar (ratified 2026-07-05): the REGIONAL RECURRING ROSTER, keyed by band culture + region.
// On any qualifying capture/encounter, the engine asks this policy whether a living nemesis of that band
// already holds that region ("you recross Ranulf") before minting a brand new identity.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure roster lookup: which (if any) living nemesis of a given band culture already claims a given
    ///   region. Minting a fresh identity when this returns null is the engine's job, not this policy's.
    /// </summary>
    public static class NemesisRosterPolicy
    {
        /// <summary>
        ///   The living nemesis of <paramref name="bandCultureKey"/> anchored to <paramref name="regionId"/>,
        ///   or null when none exists (mint a new one). Dead identities never match. If, unexpectedly, more
        ///   than one living identity qualifies (should not happen in normal play — one nemesis per
        ///   band+region at a time), the tie-break is deterministic: the most-established one
        ///   (highest <see cref="NemesisIdentity.TimesEncountered"/>) wins; a further tie falls back to the
        ///   lowest <see cref="NemesisIdentity.Id"/> (ordinal string compare), so the outcome never depends
        ///   on collection ordering or randomness.
        /// </summary>
        public static NemesisIdentity FindRecurring(IEnumerable<NemesisIdentity> roster, string bandCultureKey, string regionId)
        {
            if (roster == null) return null;

            return roster
                .Where(n => n != null
                            && n.IsAlive
                            && !n.IsTurned // a nemesis turned to the player's service is no longer an enemy captor
                            && n.BandCultureKey == bandCultureKey
                            && n.RegionId == regionId)
                .OrderByDescending(n => n.TimesEncountered)
                .ThenBy(n => n.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>TUNE: a Calradian year in campaign days (an 84-day year), the floor of the respawn cooldown.</summary>
        public const int RespawnCooldownMinDays = 84;

        /// <summary>TUNE: three Calradian years, the ceiling of the respawn cooldown rolled at a nemesis's death.</summary>
        public const int RespawnCooldownMaxDays = 252;

        /// <summary>
        ///   True when a nemesis of <paramref name="bandCultureKey"/> was recently killed and his
        ///   <see cref="NemesisIdentity.RespawnBlockedUntilDay"/> has not yet passed, so no FRESH nemesis of
        ///   that culture may be minted (the same bandits take the player as a faceless captor until then). A
        ///   living, turned, or never-blocked identity never blocks. Region-agnostic: the block is per band
        ///   CULTURE, as ratified ("another nemesis of the same type").
        /// </summary>
        public static bool IsCultureRespawnBlocked(IEnumerable<NemesisIdentity> roster, string bandCultureKey, int today)
        {
            if (roster == null) return false;

            return roster.Any(n => n != null
                                   && n.BandCultureKey == bandCultureKey
                                   && n.RespawnBlockedUntilDay.HasValue
                                   && today < n.RespawnBlockedUntilDay.Value);
        }
    }
}
