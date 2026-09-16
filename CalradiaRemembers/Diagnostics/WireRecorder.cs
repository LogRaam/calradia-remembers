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
using System.Collections.Generic;
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
      ///   The previous prefix seen PER CALLER, keyed by model.
      ///
      ///   The first version kept a single previous prefix and warned in a comment that interleaved callers
      ///   would confuse it. First run proved the warning worthless: prose and interpreter alternate on every
      ///   single turn, so every line read "diverges at 0 — 100.0% discarded" while both prefixes were in fact
      ///   perfectly stable. A verdict that is wrong in the normal case is not a caveat, it is a broken
      ///   instrument — and one a player would have sent us as evidence of a bug that did not exist.
      ///
      ///   Keyed by model because that is what separates the two callers here and needs nothing new plumbed
      ///   through. It is not a conversation id: two characters spoken to in turn on the SAME model still
      ///   compare against each other, and that shows as a genuine divergence. Read the verdict across two
      ///   consecutive turns with one character.
      /// </summary>
      private readonly Dictionary<string, string> _previousPrefixByModel =
         new Dictionary<string, string>(StringComparer.Ordinal);

      public WireRecorder(string folder, bool dumpBodies)
      {
         _folder = folder;
         _dumpBodies = dumpBodies;
      }

      public void OnRequest(LlmWireSnapshot snapshot)
      {
         if (snapshot == null) return;

         string model = snapshot.Model ?? "?";

         string previous;
         lock (_previousPrefixByModel)
         {
            _previousPrefixByModel.TryGetValue(model, out previous);
            _previousPrefixByModel[model] = snapshot.StablePrefix;
         }

         PrefixComparison cache = PromptCacheReport.Compare(previous, snapshot.StablePrefix);

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
