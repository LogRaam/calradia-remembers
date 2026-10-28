// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.test [all [budget] | name [name ...] | list | chat | mech | full]
      ///   Runs one self-test by name (e.g. cr.test scenario), several by name, or lists them (name + severity +
      ///   description) with no argument or "list". Adding a new test needs NO new console command: it registers
      ///   in CrSelfTest and is reachable here automatically.
      ///   cr.test all runs the MECHANICAL/logic self-tests (fast, free, deterministic) EXCEPT the irreversible ones,
      ///   and prints a [SELFTEST] summary; a number after it is the conversation tests' budget in seconds. The
      ///   real-LLM conversation tests are not in it (cr.rp_bench, cr.action_bench and cr.test chat own them); the
      ///   irreversible ones (declaring war, sealing one-shot scenarios) run only by name or with cr.test full, on a
      ///   throwaway save. WARNING: even cr.test all mutates relations/quests/clans: copy your save or reload after.
      ///   Gabriel (27/10/2026): "Enleve testall. Remplace-le par cr.test all": two commands for one job led to a
      ///   name typed after the wrong one, which ran the whole sweep.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("test", "cr")]
      public static string RunSelfTest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         string name = args is {Count: > 0}
            ? args[0].Trim()
            : null;

         if (string.IsNullOrEmpty(name) || name.Equals("list", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.List();

         if (name.Equals("all", StringComparison.OrdinalIgnoreCase))
         {
            int? llmBudgetSeconds = args.Count > 1 && int.TryParse(args[1], out int parsed) && parsed > 0 ? parsed : null;

            return CrSelfTest.RunAll(behavior, llmBudgetSeconds);
         }

         if (name.Equals("chat", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunChat(behavior); // only the conversation smoke tests (LLM calls)
         if (name.Equals("mech", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunMechanical(behavior); // only the mechanical tests (no API cost)
         if (name.Equals("full", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunEverything(behavior); // EVERYTHING incl. irreversible (declare war / seal) — throwaway save only

         if (args.Count > 1)
            return string.Join("\n", args.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => CrSelfTest.Run(a.Trim(), behavior)));

         return CrSelfTest.Run(name, behavior);
      }
   }
}
