// Code written by Gabriel Mailhot, 24/06/2026.
// SRP extraction from ChatViewModel (Phase 2): the two-stage memory finishing — after a scene/conversation a
// base event is recorded synchronously, then an LLM generates a richer first-person summary and replaces it
// by value match. Pure relocation of working static code (all inputs are parameters); no behaviour change.

#region

using System;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
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
   }
}
