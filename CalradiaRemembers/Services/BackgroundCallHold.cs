// Code written by Gabriel Mailhot, 15/10/2026.
// fkasad (Nexus, 15/10/2026), on a local server with small and medium models: background work (summaries, memory
// compaction, letters) slipped in between two replies, the server swapped its KV cache to the background prompt
// and back, and replies timed out. Each background call site now awaits this BEFORE its own timeout starts, so a
// call sent to the local server waits for the open conversation to close (BackgroundCallHoldPolicy, capped).
// NOT held: the transcript fold inside a conversation, which the reply itself waits on.

#region

using System.Diagnostics;
using System.Threading.Tasks;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Services
{
   /// <summary>Makes a background model call wait for the open conversation to close, on the local server only.</summary>
   public static class BackgroundCallHold
   {
      private const int PollMilliseconds = 1000;

      /// <summary>cr.test all seam: forces "a conversation is open" (true) or closed (false); null reads the chat.</summary>
      internal static bool? DebugConversationOpen;

      /// <summary>
      ///   Completes at once (on the calling thread, no hop) unless the call must wait; then completes once the
      ///   conversation closes or the cap passes, on a pool thread. Await it before starting the call's timeout.
      /// </summary>
      /// <param name="goesToLocalServer">The call's client sends to the OpenAI-Compatible (local) provider.</param>
      /// <param name="what">What waits, for the log ("summary for Rhagaea").</param>
      public static async Task WhileConversationOpenAsync(bool goesToLocalServer, string what)
      {
         if (!Holds(goesToLocalServer, 0)) return;

         Stopwatch held = Stopwatch.StartNew();
         ModLog.Info($"[BG HOLD] {what} waits for the conversation to close (local server).");

         while (Holds(goesToLocalServer, held.Elapsed.TotalSeconds))
            await Task.Delay(PollMilliseconds).ConfigureAwait(false);

         bool capped = held.Elapsed.TotalSeconds >= BackgroundCallHoldPolicy.MaxHoldSeconds;
         ModLog.Info($"[BG HOLD] {what} released after {held.Elapsed.TotalSeconds:0}s{(capped ? " (cap reached, conversation still open)" : "")}.");
      }

      private static bool Holds(bool goesToLocalServer, double heldSeconds)
         => BackgroundCallHoldPolicy.ShouldHold(new BackgroundCallHoldFacts {
            ConversationOpen = DebugConversationOpen ?? ChatManager.IsOpen,
            GoesToLocalServer = goesToLocalServer,
            HeldSeconds = heldSeconds
         });
   }
}
