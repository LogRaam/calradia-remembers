// Code written by Gabriel Mailhot, 26/07/2026.
// Wires the SDK's LlmMemoryCompressor into the mod. Until now CR injected a character's ENTIRE event history
// into every prompt (MemoryEventLimit is unbounded in Full mode), so a long-lived character kept inflating the
// context: a modder measured it past 20k tokens (iwasajedionce, 2026-07-26), which is a real brake on a local
// model. The compressor asks the model which memories later events have superseded, keeps the rest, and folds
// what it dropped into the profile's background context, so the character still knows its own past in outline.
// The SDK half decides WHAT to drop (and refuses to drop a first meeting, the most recent five, or anything
// grave); this half decides WHEN to pay for that call, and applies the result without losing what happened
// while the call was in flight.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Compression;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Memory
{
    /// <summary>Runs the LLM memory compressor for one character and applies the result safely.</summary>
    public static class MemoryCompressionService
    {
        private const int TimeoutSeconds = 90;

        // Session-only: how many events a character had when we last paid for a compression call. Kept in
        // memory rather than the save because it only guards spending, so losing it on reload costs at most
        // one extra call per character.
        private static readonly Dictionary<string, int> LastAttemptedAtCount = new(StringComparer.Ordinal);

        private static bool _busy;

        /// <summary>True while a compression call is in flight (only one at a time, they are not cheap).</summary>
        public static bool IsBusy => _busy;

        /// <summary>
        ///   The character whose memory most needs compressing right now, or null when none qualifies. Picks the
        ///   longest history so the worst offender (the character the player actually talks to) is served first.
        /// </summary>
        public static string PickCandidate(BannerlordNpcMemoryStore store)
        {
            if (store == null) return null;

            string best = null;
            var bestCount = 0;

            foreach (string id in store.ListIds())
            {
                NpcProfile p = store.Get(id);
                int count = p?.Events?.Count ?? 0;

                if (count <= bestCount) continue;

                int lastAttempt = LastAttemptedAtCount.TryGetValue(id, out int n)
                    ? n
                    : 0;

                if (!MemoryCompressionPolicy.ShouldCompress(count, lastAttempt)) continue;

                best = id;
                bestCount = count;
            }

            return best;
        }

        /// <summary>
        ///   Compresses one character's memory. Fire and forget: the call runs off the main thread and the
        ///   result is applied to a freshly fetched profile. <paramref name="force" /> bypasses the spending
        ///   policy (the console test path), never the SDK's own guardrails.
        /// </summary>
        public static async Task CompressAsync(NpcMemoryCampaignBehavior behavior, string npcId, bool force = false)
        {
            if (behavior?.Store == null || string.IsNullOrEmpty(npcId)) return;
            if (behavior.LlmServices is not {IsAvailable: true}) return;
            if (!force && !ModSettings.MemoryCompression) return; // another mod may be curating this memory
            if (_busy) return;

            NpcProfile profile = behavior.Store.Get(npcId);
            if (profile?.Events == null) return;

            int snapshotCount = profile.Events.Count;
            int lastAttempt = LastAttemptedAtCount.TryGetValue(npcId, out int n)
                ? n
                : 0;

            if (!force && !MemoryCompressionPolicy.ShouldCompress(snapshotCount, lastAttempt)) return;

            // Recorded BEFORE the call, so a second trigger cannot queue the same expensive pass twice.
            LastAttemptedAtCount[npcId] = snapshotCount;
            _busy = true;

            try
            {
                var compressor = new LlmMemoryCompressor(behavior.LlmServices.LlmClient);

                CompressionResult result;
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds)))
                {
                    result = await Task.Run(() => compressor.CompressAsync(profile, cts.Token)).ConfigureAwait(false);
                }

                if (result is not {IsSuccess: true})
                {
                    ModLog.Error($"[MEMORY] compression failed for {profile.Name}: {result?.ErrorMessage ?? "no result"}.");

                    return;
                }

                if (result.DroppedCount <= 0)
                {
                    ModLog.Info($"[MEMORY] compression kept everything for {profile.Name} ({snapshotCount} events); nothing to drop.");

                    return;
                }

                Apply(behavior, npcId, result, snapshotCount);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[MEMORY] compression threw for {npcId}: {ex.GetType().Name}: {ex.Message}");
            }
            finally { _busy = false; }
        }

        #region private

        /// <summary>
        ///   Writes the compressed history back. The compressor worked on a SNAPSHOT, so anything recorded while
        ///   the call was in flight (a conversation that ended meanwhile, a summary the queue enriched) sits past
        ///   the snapshot boundary and is carried over untouched: compression must never cost the player a
        ///   memory that was made after it started.
        /// </summary>
        private static void Apply(NpcMemoryCampaignBehavior behavior, string npcId, CompressionResult result, int snapshotCount)
        {
            NpcProfile fresh = behavior.Store.Get(npcId);
            if (fresh?.Events == null) return;

            List<NotableEvent> since = fresh.Events.Count > snapshotCount
                ? fresh.Events.Skip(snapshotCount).ToList()
                : new List<NotableEvent>();

            var rebuilt = new List<NotableEvent>(result.KeptEvents);
            rebuilt.AddRange(since);

            int before = fresh.Events.Count;
            fresh.Events.Clear();
            fresh.Events.AddRange(rebuilt);

            // The compressor is told to fold the EXISTING background context into its summary, so replacing is
            // right: keeping both would duplicate the older outline every pass.
            if (!string.IsNullOrWhiteSpace(result.BackgroundSummary))
                fresh.BackgroundContext = result.BackgroundSummary.Trim();

            behavior.Store.Set(fresh);
            ModLog.Info($"[MEMORY] compressed {fresh.Name}: {before} -> {fresh.Events.Count} events "
                      + $"({result.DroppedCount} dropped, {since.Count} added during the call kept).");
        }

        #endregion
    }
}
