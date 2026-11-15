// Code written by Gabriel Mailhot, 15/11/2026.
// VonLemberg (Discord, 15/11/2026, source audit of 2.8.10): "Compression runs asynchronously against an earlier memory
// snapshot. If another mod calls CrNpcMemory.RemoveMemory() while compression is in flight ... Apply() then rebuilds the
// event list from those retained snapshot entries, potentially resurrecting an intentionally removed memory. There may
// also be stale information in BackgroundSummary if the removed event was folded into that summary." Read in the code:
// true, and of cr.forget too. The pass used to REBUILD the history from what it kept; it now only takes OUT of the
// living history the very memories it chose to drop. Whatever happened meanwhile (a memory added, rewritten, enriched
// or taken back) therefore stands as it is, with no case to list. And when a memory the pass folded into its summary is
// no longer there, the summary speaks of something taken back: the pass is abandoned whole and tried again later.

#region

using System.Collections.Generic;
using System.Runtime.CompilerServices;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One compression pass coming back: what it read, what it kept, and the history as it stands now.</summary>
    public sealed class CompressionMerge<T> where T : class
    {
        /// <summary>The memories the pass read when it started.</summary>
        public IReadOnlyList<T> Before { get; init; }

        /// <summary>The ones among them it chose to keep; the others it folded into its summary.</summary>
        public IReadOnlyList<T> Kept { get; init; }

        /// <summary>The character's memories now, after whatever happened while the pass was away.</summary>
        public IReadOnlyList<T> Live { get; init; }
    }

    /// <summary>What to write back after a compression pass.</summary>
    public sealed class CompressionMergeOutcome<T> where T : class
    {
        /// <summary>
        ///   A memory the pass folded into its summary was taken back or rewritten meanwhile: the summary is out of date,
        ///   so nothing of this pass is applied.
        /// </summary>
        public bool Stale { get; init; }

        /// <summary>The history to keep: the living one, less the memories the pass dropped. The living one as it is when stale.</summary>
        public IReadOnlyList<T> History { get; init; }
    }

    /// <summary>Applies a compression pass to the history as it stands now, never to the one the pass started from.</summary>
    public static class CompressionMergePolicy
    {
        /// <summary>
        ///   The living history less the very memories the pass dropped (the same objects, not look-alikes). A memory
        ///   taken back meanwhile stays gone, one added or rewritten stays as it is.
        /// </summary>
        public static CompressionMergeOutcome<T> Resolve<T>(CompressionMerge<T> merge) where T : class
        {
            IReadOnlyList<T> live = merge?.Live ?? new List<T>();
            if (merge?.Before == null || merge.Kept == null) return new CompressionMergeOutcome<T> {Stale = true, History = live};

            var kept = new HashSet<T>(merge.Kept, Same<T>.Instance);
            var dropped = new HashSet<T>(Same<T>.Instance);
            foreach (T read in merge.Before)
                if (read != null && !kept.Contains(read))
                    dropped.Add(read);

            var stillThere = new HashSet<T>(live, Same<T>.Instance);
            foreach (T gone in dropped)
                if (!stillThere.Contains(gone))
                    return new CompressionMergeOutcome<T> {Stale = true, History = live};

            var history = new List<T>(live.Count);
            foreach (T memory in live)
                if (!dropped.Contains(memory))
                    history.Add(memory);

            return new CompressionMergeOutcome<T> {Stale = false, History = history};
        }

        /// <summary>The very same object: two memories with the same words on the same day are still two memories.</summary>
        private sealed class Same<TItem> : IEqualityComparer<TItem> where TItem : class
        {
            public static readonly Same<TItem> Instance = new();

            public bool Equals(TItem a, TItem b) => ReferenceEquals(a, b);

            public int GetHashCode(TItem item) => RuntimeHelpers.GetHashCode(item);
        }
    }
}
