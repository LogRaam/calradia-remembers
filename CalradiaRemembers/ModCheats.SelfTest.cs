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
      ///   cr.testall
      ///   Runs the MECHANICAL/logic self-tests (fast, free, deterministic) EXCEPT the irreversible ones,
      ///   and prints a [SELFTEST] summary. Each test exercises the real service/bridge pipeline and logs
      ///   PASS/FAIL/MANUAL. The real-LLM roleplay/conversation tests are NO LONGER run here (they are slow,
      ///   cost tokens, and flaky on a slow/over-reasoning model); the dedicated tools own that ground now:
      ///   cr.rp_bench (scene quality), cr.action_bench (the interpreter), cr.test chat (the chat smoke set).
      ///   The irreversible tests (declaring war, sealing one-shot scenarios) are held back so a routine
      ///   sweep never mutates the live campaign that way; run them via cr.test &lt;name&gt;, or cr.test full
      ///   on a throwaway save. WARNING: even this sweep mutates relations/quests/clans, copy your save or
      ///   reload when done.
      ///   cr.testall &lt;name&gt; [name ...] runs only those tests, as cr.test does (Gabriel, 27/10/2026: "cr.testall avec
      ///   une commande fait tous les tests quand meme"; a name used to be ignored and the whole sweep ran).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("testall", "cr")]
      public static string RunAllSelfTests(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         int? llmBudgetSeconds = null;
         if (args is {Count: > 0} && int.TryParse(args[0], out int parsed) && parsed > 0)
            llmBudgetSeconds = parsed;
         else if (args is {Count: > 0} && !string.IsNullOrWhiteSpace(args[0]))
            return string.Join("\n", args.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => CrSelfTest.Run(a.Trim(), behavior)));

         return CrSelfTest.RunAll(behavior, llmBudgetSeconds);
      }

      /// <summary>
      ///   cr.test [name|list]
      ///   Runs one self-test by name (e.g. cr.test scenario), or lists them (name + severity +
      ///   description) with no argument or "list". Adding a new test needs NO new console command —
      ///   it registers in CrSelfTest and is reachable here automatically.
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

         if (name.Equals("chat", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunChat(behavior); // only the conversation smoke tests (LLM calls)
         if (name.Equals("mech", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunMechanical(behavior); // only the mechanical tests (no API cost)
         if (name.Equals("full", StringComparison.OrdinalIgnoreCase))
            return CrSelfTest.RunEverything(behavior); // EVERYTHING incl. irreversible (declare war / seal) — throwaway save only

         return CrSelfTest.Run(name, behavior);
      }
   }
}
