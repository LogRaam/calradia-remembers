// Code written by Gabriel Mailhot, 19/06/2026.

#region

using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Commoners
{
    /// <summary>
    ///   In-memory cache of rumor blocks keyed by settlement. Each entry is valid
    ///   for exactly one game day — on the next day the cache misses and
    ///   <see cref="RumorGenerator" /> regenerates the block. The cache is cleared
    ///   on session launch (<see cref="Clear" />) so stale cross-session entries
    ///   never carry over. Nothing is persisted to the save file.
    /// </summary>
    public static class RumorStore
    {
        // Key: settlement StringId. Value: (generatedOnDay, pre-formatted rumor block).
        private static readonly Dictionary<string, (int day, string block)> _cache
            = new Dictionary<string, (int day, string block)>();

        /// <summary>
        ///   Returns the cached rumor block for <paramref name="settlement" /> if it
        ///   was generated today, otherwise regenerates and caches it.
        ///   Returns null when <see cref="RumorGenerator" /> finds no relevant events.
        /// </summary>
        public static string GetOrGenerate(Settlement settlement, int today)
        {
            if (settlement == null) return null;

            string id = settlement.StringId;

            if (_cache.TryGetValue(id, out var cached) && cached.day == today)
                return cached.block;

            string block = RumorGenerator.Generate(settlement, today);
            _cache[id] = (today, block);
            return block;
        }

        /// <summary>Drops all cached entries. Call on session launch.</summary>
        public static void Clear() => _cache.Clear();
    }
}
