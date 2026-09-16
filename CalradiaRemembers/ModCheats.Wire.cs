// Code written by Gabriel Mailhot, 15/09/2026.
// The console switch for the seat beside the wire.
//
// A player reported the prompt cache dying on turn two (fkasad, Nexus, 15/09/2026) after saving two
// prompts and diffing them by hand. This is that work, offered as a command: cr.wire on, play three
// turns, read the log. The next person to suspect the prompt costs too much can answer the question
// instead of estimating it.
//
// Off by default and never persisted: it writes a line per LLM call, which is exactly what a log a
// player will be asked to send should not carry by default.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.wire [on|off|dump] — reports what each LLM request costs: how much of the prompt sits behind
      ///   the cache breakpoint, and how much of it the next turn can still read. "dump" also writes each
      ///   request body beside the log so two turns can be diffed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("wire", "cr")]
      public static string Wire(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         string mode = args is {Count: > 0} ? args[0].Trim().ToLowerInvariant() : "";

         switch (mode)
         {
            case "off":
               behavior.LlmServices.WireObserver = null;

               return "cr.wire: off.";

            case "on":
            case "dump":
               string folder = Path.Combine(
                  Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                  "Mount and Blade II Bannerlord", "Logs");

               behavior.LlmServices.WireObserver = new WireRecorder(folder, dumpBodies: mode == "dump");

               return mode == "dump"
                  ? $"cr.wire: on, and writing each request body to {folder} as CalradiaRemembers_wire_NNN.json. "
                  + "Diff two consecutive turns with the same character to see the cacheable prefix move."
                  : "cr.wire: on. One [WIRE] line per request in the log. "
                  + "Read the prefix verdict on the SECOND turn with a character: \"fully reused\" is right, "
                  + "\"diverges at N\" means a per-turn fact has drifted into the cached prefix.";

            default:
               return "cr.wire <on|off|dump> — on: a [WIRE] line per request. "
                    + "dump: the same, plus each request body written beside the log. off: stop.";
         }
      }
   }
}
