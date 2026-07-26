// Code written by Gabriel Mailhot, 26/07/2026.
// Decides WHEN the memory compressor runs. A daily pass, at most one character per day, deliberately decoupled
// from the conversation itself: compressing at the moment a chat closes would race the conversation-summary
// queue (both re-fetch the same profile and write it back), and it would let a talkative day pay for several
// calls. One pass a day is a bounded cost that still contains growth, and it never competes with a summary the
// player is waiting on.

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Memory
{
    /// <summary>Runs one memory-compression pass a day, for the character whose history has grown longest.</summary>
    public sealed class MemoryCompressionBehavior : CampaignBehaviorBase
    {
        public static MemoryCompressionBehavior Instance;

        public MemoryCompressionBehavior()
        {
            Instance = this;
        }

        public override void RegisterEvents()
            => CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

        public override void SyncData(IDataStore dataStore) { } // nothing to persist: the histories are the state

        /// <summary>Runs the daily pass on demand (console test path). Returns what it decided, for the report.</summary>
        public string RunNow(bool force = false)
        {
            var behavior = NpcMemoryCampaignBehavior.Instance;

            if (behavior?.Store == null) return "No memory store yet (load a campaign first).";
            if (!force && !ModSettings.MemoryCompression) return "Memory compression is OFF in Mod Options.";
            if (MemoryCompressionService.IsBusy) return "A compression pass is already running.";

            string id = MemoryCompressionService.PickCandidate(behavior.Store);

            if (id == null)
                return "No character's memory is long enough to be worth compressing yet.";

            _ = MemoryCompressionService.CompressAsync(behavior, id, force);

            return $"Compressing the memory of '{id}' now. Watch the log for [MEMORY], it takes one LLM call.";
        }

        #region private

        private void OnDailyTick()
        {
            // The player can hand memory management to another mod (the extension API's AddMemory door), in
            // which case CR must not quietly drop what that mod just wrote.
            if (!ModSettings.MemoryCompression) return;

            var behavior = NpcMemoryCampaignBehavior.Instance;
            if (behavior?.Store == null || behavior.LlmServices is not {IsAvailable: true}) return;
            if (MemoryCompressionService.IsBusy) return;

            SafeRun.Step("MemoryCompressionBehavior.OnDailyTick", () =>
            {
                string id = MemoryCompressionService.PickCandidate(behavior.Store);

                if (id == null) return;

                _ = MemoryCompressionService.CompressAsync(behavior, id);
            });
        }

        #endregion
    }
}
