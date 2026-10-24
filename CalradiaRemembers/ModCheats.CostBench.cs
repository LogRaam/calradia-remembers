// Code written by Gabriel Mailhot, 24/10/2026.

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
      ///   cr.cost_bench [plan|run] [cap]
      ///   What the mod costs to play, per model and setup, priced from OpenRouter's own bill (fearente, 24/10/2026). Plan
      ///   by default: a run spends real credit. See Diagnostics/CostBench.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("cost_bench", "cr")]
      public static string RunCostBench(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         return CostBench.Execute(behavior, args);
      }
   }
}
