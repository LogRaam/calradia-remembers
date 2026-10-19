// Code written by Gabriel Mailhot, 21/06/2026.

#region

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CalradiaRemembers.Foundation;

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

      /// <summary>Drops all queued continuations — called when the chat closes so none leak into a later session.</summary>
      public static void Clear()
      {
         while (Queue.TryDequeue(out _)) { }
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
         int budget = Queue.Count;
         for (var i = 0; i < budget && Queue.TryDequeue(out Action action); i++)
            try { action(); }
            catch (Exception ex) { ModLog.Error($"[dispatcher] continuation threw {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Awaitable that resumes its continuation on the main thread. It always suspends,
      ///   so the hop is reliable even when the caller is already on the main thread (it
      ///   simply defers to the next drain).
      /// </summary>
      public static MainThreadAwaitable SwitchToMainThread() => default;
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