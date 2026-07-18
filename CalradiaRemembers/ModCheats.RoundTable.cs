// Code written by Gabriel Mailhot, 18/07/2026.
// Round-table audit 0.10. The council lap ships behind a kill-switch and has never been play-tested, which
// is precisely the reason it may not be flipped on: its end-to-end glue has no unit tests (net10 tests cannot
// reference the net472 mod), no self-test, and there was no way to run it at all without first editing code
// and rebuilding. That is a circular block, and this command breaks it: it reports the state the lap depends
// on, and can arm the switch for a session so the feature can actually be exercised in a real conversation.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.roundtable [on|off]
      ///   With no argument, reports whether the council is enabled, whether a chat is open, who is present,
      ///   and how many speakers a lap would give the floor to. With on/off, flips
      ///   <see cref="RoundTableController.Enabled" /> FOR THIS SESSION only (it is a code default, never
      ///   persisted), so the feature can be play-tested without a rebuild. Per-turn cost lands in the log as
      ///   [ROUNDTABLE TOKENS].
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("roundtable", "cr")]
      public static string RoundTable(List<string> args)
      {
         try
         {
            if (args is {Count: > 0})
            {
               bool on = !string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase);
               RoundTableController.Enabled = on;

               return on
                  ? "Round table ENABLED for this session. Open a chat where others are present, then use the "
                  + "button or write something that opens the floor (\"what do you all think?\"). Each speaker "
                  + "costs a full prompt: watch [ROUNDTABLE TOKENS] in the log."
                  : "Round table DISABLED. The button hides and the free-text trigger goes back to a no-op.";
            }

            ChatViewModel vm = ChatManager.ActiveViewModel;

            if (vm == null)
               return $"Round table: {(RoundTableController.Enabled ? "ENABLED" : "DISABLED (shipped default)")}. "
                    + "No chat is open, so there is nothing to report about participants. "
                    + "Use 'cr.roundtable on' to arm it for this session.";

            IReadOnlyList<string> present = vm.DebugWitnessNames();
            int speakers = present.Count + 1; // the participants plus the host

            return $"Round table: {(RoundTableController.Enabled ? "ENABLED" : "DISABLED (shipped default)")}.\n"
                 + $"Host: {vm.DebugMainSpeakerName()}\n"
                 + $"Present ({present.Count}): {(present.Count > 0 ? string.Join(", ", present) : "(nobody)")}\n"
                 + $"A lap would give the floor to up to {Math.Min(speakers, RoundTablePolicy.DefaultCap)} "
                 + $"of {speakers} (cap {RoundTablePolicy.DefaultCap}), one full LLM call each.";
         }
         catch (Exception ex) { return $"cr.roundtable failed: {ex.GetType().Name}: {ex.Message}"; }
      }
   }
}
