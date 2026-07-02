// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
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
      ///   Runs every registered end-to-end self-test EXCEPT the irreversible ones (declaring war,
      ///   sealing one-shot scenarios) and prints a [SELFTEST] summary. Each test exercises the real
      ///   service/bridge pipeline and logs PASS/FAIL/MANUAL. The irreversible tests are held back so a
      ///   routine sweep never mutates the live campaign that way (and never poisons the conversation
      ///   tests); run them explicitly via cr.test &lt;name&gt;, or cr.test full on a throwaway save.
      ///   WARNING: even the safe sweep mutates relations/quests/clans — copy your save or reload when done.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("testall", "cr")]
      public static string RunAllSelfTests(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         return CrSelfTest.RunAll(behavior);
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
