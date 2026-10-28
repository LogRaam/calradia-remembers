// Code written by Gabriel Mailhot, 17/08/2026.

#region

using System;
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
      ///   cr.test all, but far cheaper than it (short interpreter-only calls). Read the log to act on it.
      ///   Add "focus" (e.g. cr.action_bench 3 focus) to run only the curated failing/flaky slice plus its
      ///   canaries instead of all 220 cases: ~85% cheaper for a refinement loop. Run the full corpus (no "focus")
      ///   once before release, since the focus slice is blind to the cases it omits.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("action_bench", "cr")]
      public static string RunActionBench(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         var passes = 1;
         var focusOnly = false;
         if (args != null)
            foreach (string arg in args)
            {
               if (string.Equals(arg, "focus", StringComparison.OrdinalIgnoreCase)) focusOnly = true;
               else if (int.TryParse(arg, out int parsed) && parsed > 0) passes = parsed;
            }

         return ActionBench.Run(behavior, passes, focusOnly);
      }
   }
}
