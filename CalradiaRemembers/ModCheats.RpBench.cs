// Code written by Gabriel Mailhot, 12/08/2026.

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
      ///   cr.rp_bench [passes]
      ///   Roleplay MODEL bench: plays two fixed scripted scenes (a lord affronted then appeased, and an intimate
      ///   scene forced to Hardcore) through whatever LLM is configured in MCM, then writes a readable transcript
      ///   plus advisory structural signals to CalradiaRemembers_rp_bench.log. Swap the roleplay model in MCM,
      ///   re-run, and rank models BY READING. There is no automated judge: the user is the judge. Real-LLM tool
      ///   (spends tokens); deliberately NOT part of cr.test all. [passes] defaults to 1 (one clean transcript).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("rp_bench", "cr")]
      public static string RunRoleplayBench(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         int passes = 1;
         if (args != null && args.Count > 0 && int.TryParse(args[0], out int parsed) && parsed > 0)
            passes = parsed;

         return RoleplayBench.Run(behavior, passes);
      }
   }
}
