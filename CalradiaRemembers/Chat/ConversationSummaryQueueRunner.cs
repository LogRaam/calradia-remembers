// Code written by Gabriel Mailhot, 24/07/2026.
// The durable delivery layer for conversation-summary jobs. FinalizeConversationMemory used to fire the rich
// summary as a detached fire-and-forget Task: if the game closed or crashed before it returned, that summary
// was lost and only the plain "Spoke with X." base event survived. This runner turns that into a persisted
// queue (ConversationSummaryQueue, pure/unit-tested): a job is enqueued and saved with the campaign, a
// background worker drains it, and any jobs still pending at load are replayed. So a stated plan the player
// was promised survives a save/quit/crash instead of vanishing (the Nexus-reported "NPC forgot our same-day
// plan" fault, delivery half).

#region

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Owns the persisted <see cref="ConversationSummaryQueue" /> and drains it off the main thread. All queue
   ///   access is under <see cref="_gate" /> because the drain worker (thread pool) and <see cref="SyncData" />
   ///   (main thread, at save) both touch it. The LLM call itself runs OUTSIDE the lock, so a save that lands
   ///   mid-summary serializes the job as still-pending and it replays on reload (idempotent: see
   ///   <see cref="ConversationSummaryService.ProcessConversationJobAsync" />).
   /// </summary>
   public sealed class ConversationSummaryQueueRunner
   {
      // A byte[] blob keyed distinctly from the profile store's own key. Additive: a save written before this
      // feature simply has no such key, and Deserialize(null) degrades to an empty queue (save-migration rule).
      private const string SaveKey = "CR_ConvSummaryQueue";

      private readonly object _gate = new object();
      private ConversationSummaryQueue _queue = new ConversationSummaryQueue();
      private bool _draining;

      /// <summary>How many jobs are waiting, for the diagnostic command.</summary>
      public int PendingCount
      {
         get
         {
            lock (_gate) return _queue.Count;
         }
      }

      /// <summary>A snapshot of the pending jobs, for the diagnostic command.</summary>
      public IReadOnlyList<PendingSummaryJob> Snapshot()
      {
         lock (_gate) return new List<PendingSummaryJob>(_queue.Pending);
      }

      /// <summary>Enqueues a job (persisted on the next save) and kicks the worker.</summary>
      public void Enqueue(PendingSummaryJob job)
      {
         if (job == null) return;

         SummaryEnqueueOutcome outcome;
         lock (_gate) outcome = _queue.Enqueue(job);

         if (outcome == SummaryEnqueueOutcome.AddedOldestDropped)
            ModLog.Info("[CONV QUEUE] queue was full; dropped the oldest pending summary job to make room.");

         KickDrain();
      }

      /// <summary>Replays whatever survived the load: called from OnSessionLaunched, AFTER SyncData(load).</summary>
      public void ReplayOnLoad() => KickDrain();

      /// <summary>Forces a drain pass (the diagnostic command's 'drain' argument).</summary>
      public void DrainNow() => KickDrain();

      /// <summary>
      ///   Persists the queue as a UTF-8 byte[] blob, mirroring the profile store: a bad blob must never brick a
      ///   save or a load, so every step is isolated and a failed load degrades to an empty queue.
      /// </summary>
      public void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            string json;
            lock (_gate)
            {
               try { json = _queue.Serialize(); }
               catch (Exception ex)
               {
                  ModLog.Error($"[CONV QUEUE] serialize failed, persisting an empty queue this save. {ex.GetType().Name}: {ex.Message}");
                  json = "";
               }
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json ?? "");
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[CONV QUEUE] blob save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature: fall through to an empty queue */
            }

            string json = bytes is {Length: > 0}
               ? Encoding.UTF8.GetString(bytes)
               : null;
            ConversationSummaryQueue loaded = ConversationSummaryQueue.Deserialize(json);
            lock (_gate) _queue = loaded;
            ModLog.Info($"[CONV QUEUE] loaded {loaded.Count} pending summary job(s) from save.");
         }
      }

      private void KickDrain()
      {
         lock (_gate)
         {
            if (_draining) return;
            _draining = true;
         }

         _ = Task.Run(DrainLoopAsync);
      }

      private async Task DrainLoopAsync()
      {
         try
         {
            while (true)
            {
               PendingSummaryJob job;
               lock (_gate) job = _queue.NextPending();

               if (job == null) return;

               NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;

               // Infrastructure not ready (no store, or no LLM client because there is no API key yet): STOP and
               // keep every job intact. Failing them here would burn their retry budget and drop them for a
               // reason that has nothing to do with the job. They replay on the next enqueue or the next load.
               if (behavior?.Store == null || behavior.LlmServices?.LlmClient == null) return;

               bool resolved = await ConversationSummaryService.ProcessConversationJobAsync(behavior, job).ConfigureAwait(false);

               lock (_gate)
               {
                  if (resolved) _queue.Complete(job);
                  else if (_queue.Fail(job)) ModLog.Info($"[CONV QUEUE] dropped a summary job for {job.ProfileId} after repeated failures.");
               }
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[CONV QUEUE] drain loop error: {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            lock (_gate) _draining = false;
         }
      }
   }
}
