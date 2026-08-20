// Code written by Gabriel Mailhot, 19/08/2026.
// Console face of the single-call bench. Thin: parses [passes] and an optional [model] id, hands off to the runner.

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
      ///   cr.single_call_bench [passes] [modelId]
      ///   Runs the single-call bench: for each scenario, one call has the tested model WRITE the NPC reply and self-tag
      ///   it, then the SAME prose is read by the configured interpreter, and the two tag sets are compared. Reports the
      ///   share of scenarios where they AGREE (the evidence for retiring the two-call pipeline). [passes] defaults to 1
      ///   (use 3 for majority scoring); "temp=0.2" sets the SINGLE-CALL temperature (default 0.6) to test whether a
      ///   lower, extraction-friendly temperature recovers the deeds a prose-warm 0.6 drops; any other non-numeric
      ///   argument is the single-call model id, e.g. "cr.single_call_bench 3 temp=0.2 deepseek/deepseek-v4-flash-0731".
      ///   Set your interpreter model in MCM first (it is the reference cross-check). Real-LLM (spends tokens), NOT part
      ///   of cr.testall. Writes CalradiaRemembers_single_call_bench.log.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("single_call_bench", "cr")]
      public static string RunSingleCallBench(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         var passes = 1;
         string modelId = null;
         var singleCallTemp = 0.6f;
         if (args != null)
            foreach (string arg in args)
            {
               if (int.TryParse(arg, out int parsed) && parsed > 0) passes = parsed;
               else if (arg != null && arg.StartsWith("temp=", System.StringComparison.OrdinalIgnoreCase)
                        && float.TryParse(arg.Substring(5), System.Globalization.NumberStyles.Float,
                                          System.Globalization.CultureInfo.InvariantCulture, out float t) && t >= 0f)
                  singleCallTemp = t;
               else if (!string.IsNullOrWhiteSpace(arg)) modelId = arg.Trim();
            }

         return SingleCallBench.Run(behavior, passes, modelId, singleCallTemp);
      }
   }
}
