// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CalradiaRemembers.Foundation;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Foundation
{
   /// <summary>
   ///   Marshals work back onto Bannerlord's main thread. An LLM turn runs its network
   ///   call off-thread (HttpClient), but EVERYTHING after it — mutating the GauntletUI-bound
   ///   message list, executing game actions, reading campaign state — must run on the main
   ///   thread, because those APIs are not thread-safe. Touching them from the await
   ///   continuation (which, with no SynchronizationContext, resumes on a thread-pool thread)
   ///   is what caused the intermittent "crash while waiting for a response".
   ///   Usage: <c>await MainThreadDispatcher.SwitchToMainThread();</c> immediately after the
   ///   network await — the rest of the method then runs on the main thread. Continuations are
   ///   drained once per UI frame from <see cref="ChatScrollablePanel.OnLateUpdate" />, which
   ///   keeps ticking even while campaign time is paused for the chat overlay.
   /// </summary>
   public static class MainThreadDispatcher
   {
      private static readonly ConcurrentQueue<Action> Queue = new();

      // Work that belongs to the CAMPAIGN, not to the open conversation (VonLemberg, Discord 16/11/2026, audit "Dispatcher
      // Clear Drops Unrelated Async Work"): a letter reader's judgement of a secret came back from the model in the same
      // frame a chat closed, and Clear() dropped it with the conversation's own replies: no grudge, no confrontation letter,
      // no treason charge, nothing logged, while "the reader now knows" was already written. Clear() never touches this queue.
      private static readonly ConcurrentQueue<Action> Lasting = new();

      /// <summary>
      ///   Drops the conversation's queued continuations, called when the chat closes so none leak into a later session.
      ///   Work queued with <see cref="EnqueueLasting" /> or <see cref="SwitchToMainThreadLasting" /> is kept.
      /// </summary>
      public static void Clear()
      {
         while (Queue.TryDequeue(out _)) { }
      }

      /// <summary>
      ///   Queues work that must run whatever happens to the open conversation (a campaign consequence, a panel's late
      ///   text). Thread-safe. The caller checks for itself that its campaign is still the one loaded.
      /// </summary>
      public static void EnqueueLasting(Action action)
      {
         if (action != null) Lasting.Enqueue(action);
      }

      /// <summary>
      ///   Queues work the CAMPAIGN owns (a tale's meeting, an audience granted, an envoy received, a scene due): it survives
      ///   a chat closing, and is set aside, logged, if another campaign is loaded before it runs. VonLemberg (Discord
      ///   16/11/2026, addendum "Dispatcher Quest Audience Blast Radius"; Gabriel: "oui fait 2 et 3"): a tale's summoned
      ///   meeting waits frame by frame while a chat is open, in the queue that chat's close clears, so the close that would
      ///   have freed it dropped it for good. Call it on the game thread (it reads the loaded campaign).
      /// </summary>
      public static void EnqueueForCampaign(Action action)
      {
         if (action == null) return;

         Campaign asked = Campaign.Current;
         Lasting.Enqueue(() =>
         {
            if (!ReferenceEquals(asked, Campaign.Current))
            {
               ModLog.Info("[dispatcher] campaign work queued under another campaign was set aside.");

               return;
            }

            action();
         });
      }

      /// <summary>Queues a continuation to run on the next main-thread drain. Thread-safe.</summary>
      public static void Enqueue(Action action)
      {
         if (action != null) Queue.Enqueue(action);
      }

      /// <summary>
      ///   Runs queued continuations on the main thread — bounded to the count present at
      ///   entry, so a continuation that synchronously re-enqueues cannot stretch the frame.
      /// </summary>
      public static void Pump()
      {
         Drain(Queue);
         Drain(Lasting);
      }

      private static void Drain(ConcurrentQueue<Action> queue)
      {
         int budget = queue.Count;
         for (var i = 0; i < budget && queue.TryDequeue(out Action action); i++)
            try { action(); }
            catch (Exception ex) { ModLog.Error($"[dispatcher] continuation threw {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Awaitable that resumes its continuation on the main thread. It always suspends,
      ///   so the hop is reliable even when the caller is already on the main thread (it
      ///   simply defers to the next drain).
      /// </summary>
      public static MainThreadAwaitable SwitchToMainThread() => default;

      /// <summary>
      ///   As <see cref="SwitchToMainThread" />, for work that belongs to the campaign rather than to the open conversation:
      ///   its continuation survives the chat closing (<see cref="Clear" />). Check after it that the campaign is still the
      ///   one you started in.
      /// </summary>
      public static LastingMainThreadAwaitable SwitchToMainThreadLasting() => default;
   }

   /// <summary>Custom awaiter that resumes its continuation on the main thread, in the queue a chat close does not clear.</summary>
   public readonly struct LastingMainThreadAwaitable : INotifyCompletion
   {
      public LastingMainThreadAwaitable GetAwaiter() => this;
      public bool IsCompleted => false;
      public void OnCompleted(Action continuation) => MainThreadDispatcher.EnqueueLasting(continuation);
      public void GetResult() { }
   }

   /// <summary>Custom awaiter that resumes its continuation on the main-thread dispatcher.</summary>
   public readonly struct MainThreadAwaitable : INotifyCompletion
   {
      public MainThreadAwaitable GetAwaiter() => this;
      public bool IsCompleted => false; // always suspend → continuation is queued
      public void OnCompleted(Action continuation) => MainThreadDispatcher.Enqueue(continuation);
      public void GetResult() { }
   }
}