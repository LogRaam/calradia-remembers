// Code written by Gabriel Mailhot, 24/06/2026.
// SRP extraction from ChatViewModel (Phase 2): the two-stage memory finishing — after a scene/conversation a
// base event is recorded synchronously, then an LLM generates a richer first-person summary and replaces it
// by value match. Pure relocation of working static code (all inputs are parameters); no behaviour change.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Captivity;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Generates and applies the rich LLM summary that replaces a base captive/conversation memory.</summary>
   public static class ConversationSummaryService
   {
      public static async Task GenerateAndApplyCaptiveSummaryAsync(
         NpcMemoryCampaignBehavior behavior,
         string captorId,
         NotableEvent baseEvent,
         string transcript,
         string intentVerb,
         bool playerIsFemale)
      {
         try
         {
            NpcProfile captor = behavior.Store.Get(captorId);

            if (captor == null) return;

            var summarizer = new CaptiveSceneSummarizer(behavior.LlmServices.LlmClient);

            string rich;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   captor, transcript, intentVerb, playerIsFemale, ModSettings.ReplyLanguage, cts.Token))
                                .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(rich)) return;

            // Re-fetch fresh and replace the base event's summary by value match.
            NpcProfile fresh = behavior.Store.Get(captorId);

            if (fresh == null) return;
            int idx = fresh.Events.FindLastIndex(e => e.Equals(baseEvent));

            if (idx < 0) return;

            fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
            behavior.Store.Set(fresh);
            ModLog.Info($"[CAPTIVE MEMORY] rich summary applied for {captor.Name}: \"{rich.Trim()}\"");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CAPTIVE MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      public static async Task GenerateAndApplyConversationSummaryAsync(
         NpcMemoryCampaignBehavior behavior,
         string npcId,
         NotableEvent baseEvent,
         string transcript,
         string playerName)
      {
         try
         {
            NpcProfile npc = behavior.Store.Get(npcId);

            if (npc == null) return;

            var summarizer = new ConversationSummarizer(behavior.LlmServices.LlmClient);

            string rich;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   npc, transcript, playerName, ModSettings.ReplyLanguage, cts.Token))
                                .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(rich)) return;

            // Re-fetch fresh and replace the base event's summary by value match.
            NpcProfile fresh = behavior.Store.Get(npcId);

            if (fresh == null) return;
            int idx = fresh.Events.FindLastIndex(e => e.Equals(baseEvent));

            if (idx < 0) return;

            fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
            behavior.Store.Set(fresh);
            ModLog.Info($"[CONV MEMORY] rich summary applied for {npc.Name}: \"{rich.Trim()}\"");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV MEMORY] summary generation failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Processes ONE durable <see cref="PendingSummaryJob" /> from the summary queue: rebuilds the anchor
      ///   event from the job's stored fields (the live objects are gone after a reload), generates the rich
      ///   summary, and replaces the base event by value match. Returns TRUE when the job is RESOLVED and must
      ///   be dropped from the queue: applied, or nothing left to do (the NPC is gone, the anchor no longer
      ///   matches because it was already enriched, or the model returned nothing worth retrying). Returns FALSE
      ///   only on a transient LLM failure (timeout / error / thrown), so the queue retries it up to its cap.
      ///   This is the worker half of the crash-safe pipeline: it is idempotent, because once the summary has
      ///   replaced the base event the anchor stops matching, so a replay after a reload is a harmless no-op.
      /// </summary>
      public static async Task<bool> ProcessConversationJobAsync(NpcMemoryCampaignBehavior behavior, PendingSummaryJob job, CancellationToken ct = default)
      {
         if (behavior?.Store == null || job == null) return true;

         NpcProfile npc = behavior.Store.Get(job.ProfileId);

         if (npc == null) return true;                                          // the NPC no longer exists: drop.
         if (!Enum.TryParse(job.AnchorType, out NotableEventType anchorType)) return true; // unparseable anchor: drop.
         if (behavior.LlmServices?.LlmClient == null) return false;             // no client right now: retry later.

         try
         {
            var summarizer = new ConversationSummarizer(behavior.LlmServices.LlmClient);

            string rich;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct))
            {
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   npc, job.Transcript, job.PlayerName, job.ReplyLanguage, linked.Token))
                                .ConfigureAwait(false);
            }

            // No text came back. This is either a transient LLM failure (a 403 / timeout / 500 the client
            // reports as an unsuccessful response rather than a thrown exception) or a genuinely empty result;
            // we cannot tell them apart here. For a DURABLE queue the safe default is to RETRY, so a brief API
            // blip does not silently drop a promised plan. The base "Spoke with X." event still stands in the
            // meantime, and the queue's own attempt cap drops a job that keeps coming back empty, so this never
            // spins forever.
            if (string.IsNullOrWhiteSpace(rich)) return false;

            NpcProfile fresh = behavior.Store.Get(job.ProfileId);

            if (fresh == null) return true;

            var anchor = new NotableEvent(job.AnchorDay, anchorType, job.AnchorSummary);
            int idx = fresh.Events.FindLastIndex(e => e.Equals(anchor));

            if (idx < 0) return true;                                           // already enriched (or gone): idempotent no-op.

            fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
            behavior.Store.Set(fresh);
            ModLog.Info($"[CONV MEMORY] queued summary applied for {npc.Name}: \"{rich.Trim()}\"");

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV MEMORY] queued summary failed (will retry): {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }
   }
}
