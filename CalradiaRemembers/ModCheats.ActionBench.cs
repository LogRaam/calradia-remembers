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
      ///   cr.action_bench [passes]
      ///   Interpreter extraction bench: runs every ActionBenchCatalog case (a hand-written NPC reply paired with
      ///   the action(s) it should, or must not, produce) through the configured interpreter model, scores each, and
      ///   writes a per-case scoreboard to CalradiaRemembers_action_bench.log. Misses and false positives point at
      ///   the tell or anti-pattern to tighten in GameActionCatalog. [passes] defaults to 1; pass a higher count
      ///   (e.g. cr.action_bench 3) to run each case several times and judge it by MAJORITY, so a nondeterministic
      ///   model's dice do not read as a stable failure. Real-LLM tool (spends tokens), deliberately NOT part of
      ///   cr.testall, but far cheaper than it (short interpreter-only calls). Read the log to act on it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("action_bench", "cr")]
      public static string RunActionBench(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         int passes = 1;
         if (args != null && args.Count > 0 && int.TryParse(args[0], out int parsed) && parsed > 0)
            passes = parsed;

         return ActionBench.Run(behavior, passes);
      }
   }
}
