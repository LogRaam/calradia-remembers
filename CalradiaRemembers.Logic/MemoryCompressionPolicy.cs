// Code written by Gabriel Mailhot, 26/07/2026.
// Decides WHEN it is worth paying for the SDK's LLM-driven memory compressor (LlmMemoryCompressor), which
// asks the model which events are superseded and can be dropped. CR injects an NPC's entire event history
// into every prompt, so a long-lived character's context keeps growing (a modder measured past 20k tokens),
// but compression itself costs one extra LLM call, so it must not run on every conversation. This policy
// owns only the timing decision (a raw event count against a threshold, plus a retry-growth guard against
// paying for a fruitless pass over and over); the compressor itself decides WHAT to drop.
// 14/08/2026: player report "memory compression is still being carried out for deceased characters" (a dead
// character is never spoken to again, so no future conversation ever reads its memory), and paying for a
// compression call on it is pure waste. Added an isAlive gate that refuses outright, checked before the
// threshold/retry logic below.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure timing policy for when to run the LLM memory compressor: below the threshold it never runs,
    ///   and above it, it runs at most once per handful of new events so a fruitless pass (the model kept
    ///   everything) cannot turn into a fresh paid call after every single new memory.
    /// </summary>
    public static class MemoryCompressionPolicy
    {
        /// <summary>The default event count above which compression becomes worth its extra LLM call.</summary>
        public const int DefaultThreshold = 24;

        /// <summary>
        ///   How many new events must accumulate after a fruitless attempt before retrying it. Without this
        ///   guard, a compression pass that drops nothing (the model judged every event still relevant)
        ///   leaves the event count unchanged, so the next conversation would immediately re-trigger the
        ///   same paid call, and the one after that, forever. Requiring a few new events before retrying
        ///   caps a fruitless pass at costing at most one call per handful of new memories.
        /// </summary>
        public const int RetryGrowth = 4;

        /// <summary>
        ///   True when the NPC's memory is worth compressing right now, using <see cref="DefaultThreshold" />.
        /// </summary>
        /// <param name="eventCount">The NPC's current total event count.</param>
        /// <param name="lastAttemptedAtCount">
        ///   The event count at the time compression was last attempted for this NPC, or 0/negative if
        ///   compression has never been attempted.
        /// </param>
        public static bool ShouldCompress(int eventCount, int lastAttemptedAtCount)
            => ShouldCompress(eventCount, lastAttemptedAtCount, DefaultThreshold, isAlive: true);

        /// <summary>
        ///   True when the NPC's memory is worth compressing right now, using <see cref="DefaultThreshold" />
        ///   and assuming the character is alive. See the 4-argument overload for the full order of checks.
        /// </summary>
        /// <param name="eventCount">The NPC's current total event count.</param>
        /// <param name="lastAttemptedAtCount">
        ///   The event count at the time compression was last attempted for this NPC, or 0/negative if
        ///   compression has never been attempted.
        /// </param>
        /// <param name="threshold">
        ///   The event count above which compression becomes worth it. A value below 1 is treated as
        ///   <see cref="DefaultThreshold" />, so a misconfigured tuning value cannot turn compression into
        ///   an every-message LLM call.
        /// </param>
        public static bool ShouldCompress(int eventCount, int lastAttemptedAtCount, int threshold)
            => ShouldCompress(eventCount, lastAttemptedAtCount, threshold, isAlive: true);

        /// <summary>
        ///   True when the NPC's memory is worth compressing right now. Order: (1) not <paramref name="isAlive" />,
        ///   never (a dead character is never spoken to again, so no future conversation ever reads its
        ///   memory, and paying for a compression call on it is pure waste, checked first, before the
        ///   threshold/retry logic even looks at the history); (2) below <paramref name="threshold" />, never
        ///   (not worth the extra LLM call yet); (3) never attempted before for this NPC, always compress;
        ///   (4) attempted before, only once at least <see cref="RetryGrowth" /> new events have accumulated
        ///   since that attempt (see <see cref="RetryGrowth" /> for why); (5) otherwise, not yet.
        /// </summary>
        /// <param name="eventCount">The NPC's current total event count.</param>
        /// <param name="lastAttemptedAtCount">
        ///   The event count at the time compression was last attempted for this NPC, or 0/negative if
        ///   compression has never been attempted.
        /// </param>
        /// <param name="threshold">
        ///   The event count above which compression becomes worth it. A value below 1 is treated as
        ///   <see cref="DefaultThreshold" />, so a misconfigured tuning value cannot turn compression into
        ///   an every-message LLM call.
        /// </param>
        /// <param name="isAlive">False for a dead (or otherwise gone) character: compression is refused outright.</param>
        public static bool ShouldCompress(int eventCount, int lastAttemptedAtCount, int threshold, bool isAlive)
        {
            if (!isAlive)
            {
                return false;
            }

            int effectiveThreshold = threshold < 1 ? DefaultThreshold : threshold;

            if (eventCount < effectiveThreshold)
            {
                return false;
            }

            if (lastAttemptedAtCount <= 0)
            {
                return true;
            }

            return eventCount >= lastAttemptedAtCount + RetryGrowth;
        }
    }
}
