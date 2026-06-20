// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Keeps the world-event log bounded, with two lifetimes on purpose: third-party events are
    ///   rumour fodder and age out (a window, plus a hard cap), while the player's own deeds are the
    ///   chronicle and are kept long-term (only a very high safety cap). Pure — operates on any list of
    ///   <see cref="IRumorRecord"/> so it is unit-testable independent of the engine.
    /// </summary>
    public static class WorldEventPruner
    {
        /// <summary>Third-party rumours older than this many days are forgotten.</summary>
        public const int RumorWindowDays = 42;

        /// <summary>Hard cap on retained third-party rumours (oldest dropped first).</summary>
        public const int MaxRumorEvents = 200;

        /// <summary>Safety cap on the chronicle itself (only an extreme saga reaches it).</summary>
        public const int MaxPlayerEvents = 1000;

        /// <summary>Prunes <paramref name="events"/> in place to the bounds above.</summary>
        public static void Prune<T>(List<T> events, int today) where T : IRumorRecord
        {
            if (events == null) return;

            // Stale third-party rumours fade (player deeds never age out).
            events.RemoveAll(e => !e.InvolvesPlayer && today - e.Day > RumorWindowDays);

            CapOldest(events, e => !e.InvolvesPlayer, MaxRumorEvents);
            CapOldest(events, e => e.InvolvesPlayer, MaxPlayerEvents);
        }

        private static void CapOldest<T>(List<T> events, System.Func<T, bool> belongsToGroup, int max)
            where T : IRumorRecord
        {
            List<T> group = events.Where(belongsToGroup).ToList();
            if (group.Count <= max) return;

            foreach (T old in group.OrderBy(e => e.Day).Take(group.Count - max))
                events.Remove(old);
        }
    }
}
