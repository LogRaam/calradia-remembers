// Code written by Gabriel Mailhot, 12/09/2026.
// The console entry for the knowledge bench. Gabriel wanted it runnable at the keyboard, in a live game,
// rather than through a dotnet test with an environment variable set.

#region

using System.Collections.Generic;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.memory_bench [name] — asks a character six questions against the REAL model and reports whether he
      ///   used what he knows and kept quiet about what he does not. Spends tokens, which is why it is a command
      ///   rather than part of cr.test all.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("memory_bench", "cr")]
      public static string RunMemoryBench(List<string> args)
      {
         if (!TryContext(out Behaviors.NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         string who = args is {Count: > 0} ? string.Join(" ", args).Trim() : null;

         return MemoryBench.Run(behavior, who);
      }
   }
}
