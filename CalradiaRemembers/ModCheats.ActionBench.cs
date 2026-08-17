// Code written by Gabriel Mailhot, 17/08/2026.

#region

using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.action_bench
      ///   Interpreter extraction bench: runs every ActionBenchCatalog case (a hand-written NPC reply paired with
      ///   the action it should, or must not, produce) through the configured interpreter model, scores each, and
      ///   writes a per-case scoreboard to CalradiaRemembers_action_bench.log. Misses and false positives point at
      ///   the tell or anti-pattern to tighten in GameActionCatalog. Real-LLM tool (spends tokens), deliberately
      ///   NOT part of cr.testall, but far cheaper than it (short interpreter-only calls). Read the log to act on it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("action_bench", "cr")]
      public static string RunActionBench(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         return ActionBench.Run(behavior);
      }
   }
}
