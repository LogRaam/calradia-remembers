// Code written by Gabriel Mailhot, 15/11/2026.
// VonLemberg (Discord, 15/11/2026, source audit of 2.8.10): "CompanionAudienceTopicResolver.CallbackKey() identifies
// memories using only companionId:gameDay:type. Two distinct memories of the same type recorded on the same day
// therefore receive identical keys. Once one is used for a Callback audience, the other may be permanently suppressed
// ... Please preserve existing spend-once behavior and compatibility with saved UsedCallbackKeys." Read in the code:
// true. A memory spent from now on is remembered by a key that tells it from its twin of the same day; a key saved by
// an older version still stands for every memory of that companion, day and kind, since it never recorded which one.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>One stored memory, as the callback ledger names it.</summary>
    public sealed class CallbackMemory
    {
        /// <summary>The key every version up to 2.8.10 saved: companion, day and kind.</summary>
        public string DayAndKindKey { get; init; }

        /// <summary>The memory's own identity when it has one (null on older memories).</summary>
        public string MemoryId { get; init; }

        /// <summary>Its words, which tell it from another of the same day and kind when it has no identity.</summary>
        public string Summary { get; init; }
    }

    /// <summary>Names a memory spent as a callback, so it is called back to once and its twin of the same day is not lost.</summary>
    public static class CallbackKeyPolicy
    {
        /// <summary>The key to save when this memory is spent: the older key, then what tells this memory from its twins.</summary>
        public static string KeyOf(CallbackMemory m)
        {
            if (m == null) return "";

            string own = string.IsNullOrEmpty(m.MemoryId)
                ? "w" + WordsHash(m.Summary ?? "").ToString("x8")
                : "i" + m.MemoryId;

            return $"{m.DayAndKindKey}:{own}";
        }

        // FNV-1a, the same number in every session and on every machine (string.GetHashCode is not). Its own copy: the
        // one in Logic.Romance would tie Companions and Romance into a cycle.
        private static uint WordsHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return hash;
        }

        /// <summary>Whether this memory was already called back to: by its own key, or by the key an older version saved.</summary>
        public static bool IsSpent(CallbackMemory m, System.Collections.Generic.ICollection<string> spent)
        {
            if (m == null || spent == null) return false;

            return spent.Contains(KeyOf(m)) || spent.Contains(m.DayAndKindKey ?? "");
        }
    }
}
