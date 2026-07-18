// Code written by Gabriel Mailhot, 17/07/2026.
// Courier audit 3.1 (thread race): the LLM letter-generation continuations resume on the THREAD POOL (a campaign
// has no SynchronizationContext), and used to mutate profile.SentLetters and the store directly from there, while
// the main thread was enumerating those same lists on its daily/courier ticks. A List mutated mid-enumeration
// throws "Collection was modified", swallowed by SafeRun, so the whole daily pass aborts (couriers unspawned,
// deliveries skipped) with no visible trace, and repeated races can corrupt the save. This tiny queue marshals
// every post-LLM mutation back onto the main thread: the continuation only PRODUCES content and POSTs a closure;
// the closure runs on the next tick drain, in the same single-threaded world the readers live in.

#region

using System;
using System.Collections.Concurrent;
using CalradiaRemembers.Diagnostics;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Marshals letter-generation mutations from LLM continuation threads onto the main game thread.</summary>
   public static class LetterMutationQueue
   {
      private static readonly ConcurrentQueue<Action> Pending = new();

      /// <summary>Enqueues a mutation to run on the next main-thread drain. Safe to call from any thread.</summary>
      public static void Post(Action mutation)
      {
         if (mutation != null) Pending.Enqueue(mutation);
      }

      /// <summary>
      ///   Runs every queued mutation, on the main thread (called from a game tick). Each runs under its own
      ///   guard so one failing closure never drops the rest, and never throws into the tick.
      /// </summary>
      public static void DrainOnMainThread()
      {
         while (Pending.TryDequeue(out Action mutation))
            SafeRun.Step("LetterMutationQueue.Drain", mutation);
      }

      /// <summary>Session-launch hygiene: drop any stale closures captured against a previous campaign's objects.</summary>
      public static void Clear()
      {
         while (Pending.TryDequeue(out _))
         {
            // discard
         }
      }
   }
}
