// Code written by Gabriel Mailhot, 15/09/2026.
// "What did it cost, and why?" — a question that had an answer only if a player did the work by hand.
//
// fkasad (Nexus, 15/09/2026) found the prompt cache dying on turn two by saving two successive prompts and
// diffing them himself. He was right, and nothing in the mod was watching. This is that measurement, taken
// automatically, one line per request:
//
//   [WIRE] z-ai/glm-4.6 | system 51,204 (prefix 47,713 + tail 3,491) | messages 2,140 in 7
//          | prefix: 47713 chars, fully reused
//
// The line names the two numbers that decide a bill and nothing else: how much of the prompt is behind the
// cache breakpoint, and how much of it the NEXT turn can still read. A "diverges at N — 93.2% discarded"
// where a "fully reused" belongs is the defect, stated as it happens rather than inferred from an invoice.
//
// OFF by default. It is a diagnostic, and a diagnostic that runs unasked is just noise in a log a player
// will be asked to send.

#region

using System;
using System.IO;
using System.Threading;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Prompts;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>
   ///   Reports what each outgoing request costs, and writes the bodies to disk when asked so two turns
   ///   can be diffed the way the player who found this had to do it.
   /// </summary>
   public sealed class WireRecorder : ILlmWireObserver
   {
      private readonly bool _dumpBodies;
      private readonly string _folder;
      private int _sequence;

      /// <summary>
      ///   The previous request's cacheable prefix. Compared against the next one — which is exactly the
      ///   diff a player does between two saved prompts, and carries the same caveat: several characters
      ///   speaking in turn (a room of witnesses, the interpreter) interleave here, so a "broken" verdict
      ///   between two DIFFERENT speakers is expected and means nothing. The verdict to read is the one
      ///   between two consecutive turns with the same character.
      /// </summary>
      private string _previousPrefix;

      public WireRecorder(string folder, bool dumpBodies)
      {
         _folder = folder;
         _dumpBodies = dumpBodies;
      }

      public void OnRequest(LlmWireSnapshot snapshot)
      {
         if (snapshot == null) return;

         PrefixComparison cache = PromptCacheReport.Compare(_previousPrefix, snapshot.StablePrefix);
         _previousPrefix = snapshot.StablePrefix;

         int n = Interlocked.Increment(ref _sequence);

         ModLog.Info($"[WIRE] #{n} {snapshot.Model ?? "?"}"
                   + $" | system {snapshot.SystemChars:N0}"
                   + $" (prefix {snapshot.StablePrefix?.Length ?? 0:N0} + tail {snapshot.DynamicTail?.Length ?? 0:N0})"
                   + $" | messages {snapshot.MessageChars:N0} in {snapshot.MessageCount}"
                   + $"{(snapshot.CachingRequested ? "" : " | CACHING OFF")}"
                   + $" | {cache}");

         if (!_dumpBodies) return;

         try
         {
            File.WriteAllText(Path.Combine(_folder, $"CalradiaRemembers_wire_{n:D3}.json"), snapshot.Json ?? "");
         }
         catch (Exception ex)
         {
            // A diagnostic that cannot write must not become a second problem on top of the first.
            ModLog.Info($"[WIRE] could not write body #{n}: {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
