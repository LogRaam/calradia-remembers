// Code written by Gabriel Mailhot, 17/07/2026.
// Courier audit 3.1 (thread race): the LLM letter-generation continuations resume on the THREAD POOL (a campaign
// has no SynchronizationContext), and used to mutate profile.SentLetters and the store directly from there, while
// the main thread was enumerating those same lists on its daily/courier ticks. A List mutated mid-enumeration
// throws "Collection was modified", swallowed by SafeRun, so the whole daily pass aborts (couriers unspawned,
// deliveries skipped) with no visible trace, and repeated races can corrupt the save. This tiny queue marshals
// every post-LLM mutation back onto the main thread: the continuation only PRODUCES content and POSTs a closure;
// the closure runs on the next tick drain, in the same single-threaded world the readers live in.
//
// VonLemberg (Discord 16/11/2026, audit "Late Letter Completion Cross Campaign"; Gabriel: "oui fait 2 et 3"): the queue
// was cleared once at session launch, but a reply still being written when another save was loaded posted its closure
// AFTER that clear, and the new campaign's hourly tick ran it: a companion of the same id recalled from patrol by a letter
// that campaign never sent. Every mutation now carries the campaign that asked for it, captured when the letter's request
// began, and a mutation from another campaign is set aside at the drain, logged, never run.

#region

using System;
using System.Collections.Concurrent;
using CalradiaRemembers.Foundation;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>Marshals letter-generation mutations from LLM continuation threads onto the main game thread.</summary>
   public static class LetterMutationQueue
   {
      private static readonly ConcurrentQueue<(Campaign Owner, Action Mutation)> Pending = new();

      /// <summary>
      ///   Enqueues a mutation to run on the next main-thread drain, for <paramref name="owner" />: the campaign loaded when
      ///   the letter's request began (read on the game thread, before any await). Safe to call from any thread.
      /// </summary>
      public static void Post(Campaign owner, Action mutation)
      {
         if (mutation != null) Pending.Enqueue((owner, mutation));
      }

      /// <summary>
      ///   Runs every queued mutation, on the main thread (called from a game tick). Each runs under its own
      ///   guard so one failing closure never drops the rest, and never throws into the tick.
      /// </summary>
      public static void DrainOnMainThread()
      {
         while (Pending.TryDequeue(out (Campaign Owner, Action Mutation) item))
         {
            if (!BelongsToTheLoadedCampaign(item.Owner))
            {
               ModLog.Info("[LETTER] a letter's late result from another campaign was set aside, not applied.");

               continue;
            }

            SafeRun.Step("LetterMutationQueue.Drain", item.Mutation);
         }
      }

      /// <summary>True when a mutation asked for under <paramref name="owner" /> may run in the campaign loaded now.</summary>
      internal static bool BelongsToTheLoadedCampaign(Campaign owner) => owner != null && ReferenceEquals(owner, Campaign.Current);

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
