// Code written by Gabriel Mailhot, 24/06/2026.
// SRP extraction from ChatViewModel (Phase 2): the two-stage memory finishing — after a scene/conversation a
// base event is recorded synchronously, then an LLM generates a richer first-person summary and replaces it
// by value match. Pure relocation of working static code (all inputs are parameters); no behaviour change.

#region

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Conversation;
using CalradiaRemembers.Logic.Providers;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Captivity;
using NpcMemoryService.Core.Knowledge;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;

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

            var summarizer = new CaptiveSceneSummarizer(behavior.LlmServices.HousekeepingClient);

            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"captive-scene summary for {captor.Name}");

            string rich;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds))))
            {
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   captor, transcript, intentVerb, playerIsFemale, ModSettings.ReplyLanguage, cts.Token))
                                .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(rich)) return;

            // Re-fetch fresh and replace the base event's summary by value match.
            NpcProfile fresh = behavior.Store.Get(captorId);

            if (fresh == null) return;
            int idx = fresh.IndexOfAnchor(baseEvent.Id, baseEvent);

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

            var summarizer = new ConversationSummarizer(behavior.LlmServices.HousekeepingClient);

            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"summary for {npc.Name}");

            string rich;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds))))
            {
               bool? playerIsFemale = Hero.MainHero?.IsFemale;
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   npc, transcript, playerName, ModSettings.ReplyLanguage, cts.Token, playerIsFemale))
                                .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(rich)) return;

            // Re-fetch fresh and replace the base event's summary by value match.
            NpcProfile fresh = behavior.Store.Get(npcId);

            if (fresh == null) return;
            int idx = fresh.IndexOfAnchor(baseEvent.Id, baseEvent);

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
      ///   Reads the finished conversation back for facts the character asserted that nobody had told him, and
      ///   offers them to the world (Gabriel, 2026-09-12: "lorsqu'un nouveau fait est déclaré par un NPC et
      ///   jugé crédible, alors il peut être injecté comme fait divers dans le World Events").
      ///   <para>
      ///     Runs on the SAME background path as the summary above and is deliberately independent of it: a
      ///     failed summary must not cost the claims, and a failed harvest must never cost the memory. It is
      ///     its own LLM call - the summariser's output contract is a memory line and widening it would put
      ///     the memory at risk to save a request - so the cost is one call per conversation CLOSE, never per
      ///     turn.
      ///   </para>
      /// </summary>
      public static async Task HarvestWorldClaimsAsync(
         NpcMemoryCampaignBehavior behavior,
         string npcId,
         string transcript)
      {
         // Off by default, and checked BEFORE anything else: with the switch off the extraction call is never
         // made at all, so the feature costs nothing rather than costing a request and discarding the answer.
         if (!ModSettings.EnableNpcWorldClaims) return;

         try
         {
            NpcProfile npc = behavior?.Store?.Get(npcId);
            if (npc == null || string.IsNullOrWhiteSpace(transcript)) return;

            Hero speaker = Hero.FindFirst(h => h != null && h.StringId == npcId);

            // The player never authors the world. Checked here as well as in the injector, because this is
            // the rule that must not be lost to a refactor of either side.
            if (speaker == null || speaker == Hero.MainHero) return;

            var extractor = new WorldClaimExtractor(behavior.LlmServices.HousekeepingClient);

            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"world-claim reading for {npc.Name}");

            IReadOnlyList<WorldClaim> claims;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds))))
            {
               claims = await Task.Run(() => extractor.ExtractAsync(npc, transcript, cts.Token))
                                  .ConfigureAwait(false);
            }

            if (claims == null || claims.Count == 0) return;

            int adopted = WorldClaimInjector.Adopt(claims, speaker, behavior.GameStateBridge.GetCurrentGameDay());

            ModLog.Info($"[CLAIM] {npc.Name}: {claims.Count} claim(s) read, {adopted} adopted into the world.");
         }
         catch (Exception ex)
         {
            // Where it threw (superdeeplearningaibot, Discord 10/11/2026: a bare NullReferenceException said nothing).
            string where = ex.StackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            ModLog.Error($"[CLAIM] harvest failed: {ex.GetType().Name}: {ex.Message}" + (string.IsNullOrEmpty(where) ? "" : $" ({where})"));
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
         if (behavior.LlmServices?.HousekeepingClient == null) return false;             // no client right now: retry later.

         try
         {
            var summarizer = new ConversationSummarizer(behavior.LlmServices.HousekeepingClient);

            await Services.BackgroundCallHold.WhileConversationOpenAsync(Services.LlmServices.RunsLocally(ServiceRole.Housekeeping), $"queued summary for {npc.Name}");

            string rich;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(CalradiaRemembers.Logic.BackgroundCallTimeout.Seconds(ModSettings.LlmResponseTimeoutSeconds))))
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct))
            {
               bool? playerIsFemale = Hero.MainHero?.IsFemale;
               rich = await Task.Run(() => summarizer.SummarizeAsync(
                                   npc, job.Transcript, job.PlayerName, job.ReplyLanguage, linked.Token, playerIsFemale, job.HostName))
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

            // By IDENTITY when the job carries one, which is every job enqueued since 16/09/2026. The value
            // fallback below it only serves jobs replayed from an older save.
            var anchor = new NotableEvent(job.AnchorDay, anchorType, job.AnchorSummary);
            int idx = fresh.IndexOfAnchor(job.AnchorId, anchor);

            if (idx < 0) return true;                                           // already enriched (or gone): idempotent no-op.

            fresh.Events[idx] = fresh.Events[idx] with {summary = rich.Trim()};
            behavior.Store.Set(fresh);
            ModLog.Info($"[CONV MEMORY] queued summary applied for {npc.Name}: \"{rich.Trim()}\"");

            // Read the same transcript back for facts this character asserted that nobody had told him, and
            // offer them to the world. Hooked HERE, on the durable queue's success path, so it runs once per
            // conversation rather than once per retry - and awaited rather than fired, so a claim cannot
            // outlive the job that produced it. Its own failures are swallowed inside, so it can never turn a
            // successful memory into a retry.
            await HarvestWorldClaimsAsync(behavior, job.ProfileId, job.Transcript).ConfigureAwait(false);

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
