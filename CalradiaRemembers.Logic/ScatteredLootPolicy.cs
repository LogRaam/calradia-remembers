// Code written by Gabriel Mailhot, 24/07/2026.
// The two pure decisions the scattered-loot recovery needs: WHETHER a player-won battle against a bandit band
// yields back a piece this time (a chance, so recovery is spread across several fights rather than dumping the
// whole stock the moment the player beats one band), and WHICH piece it returns (the oldest loss of a matching
// culture, so a scattered stock drains in the order it was taken). The host owns the dice and the item
// grant/strip; this only judges. Every constant is a tuning knob.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for recovering scattered captivity loot from defeated bandit bands.</summary>
    public static class ScatteredLootPolicy
    {
        /// <summary>
        ///   TUNE: the percent chance that a player-won battle against a bandit band of a matching culture
        ///   returns ONE scattered piece. Below 100 on purpose, so a stock is recovered over SEVERAL fights
        ///   (the ratified "an item in one band, another in the next"), not all at once from a single victory.
        /// </summary>
        public const int RecoveryChancePercent = 55;

        /// <summary>True when a recovery roll (0..99) succeeds this battle. A negative or out-of-range roll never recovers.</summary>
        public static bool RollRecovers(int rollPercent)
            => rollPercent >= 0 && rollPercent < RecoveryChancePercent;

        /// <summary>
        ///   The scattered piece a victory over the given cultures returns: the OLDEST-lost item whose culture
        ///   is among <paramref name="defeatedCultures" /> (drains a stock in loss order), or null when nothing
        ///   scattered matches. Case-insensitive on the culture key; a null/empty ledger or culture set yields
        ///   null. Deterministic (oldest LostDay, then first in list) so the host never has to break ties.
        /// </summary>
        public static ScatteredLootItem SelectRecoverable(
            IEnumerable<ScatteredLootItem> scattered, IEnumerable<string> defeatedCultures)
        {
            if (scattered == null || defeatedCultures == null) return null;

            var cultures = new HashSet<string>(
                defeatedCultures.Where(c => !string.IsNullOrEmpty(c)),
                System.StringComparer.OrdinalIgnoreCase);

            if (cultures.Count == 0) return null;

            return scattered
                .Where(s => s?.Item != null && s.CultureKey != null && cultures.Contains(s.CultureKey))
                .OrderBy(s => s.LostDay)
                .FirstOrDefault();
        }
    }
}
