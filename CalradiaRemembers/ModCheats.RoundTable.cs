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
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
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

      /// <summary>
      ///   cr.council_test [n|off]
      ///   Assembly spike (2026-07-20): validates the SINGLE-CALL multi-speaker council model without hiring
      ///   anyone. Open a chat with ANY NPC, run this, and it seats N members (default 3, real lords by clan
      ///   tier, synthetic fallback) around that host and arms <see cref="RoundTableController.CouncilTurnsForced" />.
      ///   Then every message you send is ONE LLM call that yields the host's reply PLUS a [WITNESS_REACTION]
      ///   from each seated member who speaks or gestures (others stay silent). This is Gabriel's whole-table
      ///   model, distinct from cr.roundtable (which runs the per-NPC LAP). 'off' disarms it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("council_test", "cr")]
      public static string CouncilTest(List<string> args)
      {
         try
         {
            if (args is {Count: > 0} && string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase))
            {
               RoundTableController.CouncilTurnsForced = false;

               return "Council mode DISARMED. Normal turns return to the standard framing.";
            }

            ChatViewModel vm = ChatManager.ActiveViewModel;

            if (vm == null)
               return "No chat is open. Talk to any NPC first, then run cr.council_test [n] (default 3).";

            var count = 3;
            if (args is {Count: > 0} && int.TryParse(args[0], out int n) && n > 0) count = Math.Min(n, 6);

            string hostName = vm.DebugMainSpeakerName();
            var added = new List<string>();

            foreach (Hero hero in Hero.AllAliveHeroes
                        .Where(h => h != Hero.MainHero && h.IsAlive && h.IsLord)
                        .Where(h => !string.Equals(h.Name?.ToString(), hostName, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(h => h.Clan?.Tier ?? 0)
                        .Take(count))
            {
               vm.AddWitness(new WitnessEntry {Name = hero.Name.ToString(), RelationToNpc = "a fellow member of this council"});
               added.Add(hero.Name.ToString());
            }

            // Synthetic fallback if the world somehow has too few lords (never in a live campaign).
            while (added.Count < count)
            {
               string name = $"Councillor {added.Count + 1}";
               vm.AddWitness(new WitnessEntry {Name = name, RelationToNpc = "a fellow member of this council"});
               added.Add(name);
            }

            RoundTableController.CouncilTurnsForced = true;

            return $"Council armed around {hostName} with {added.Count} member(s): {string.Join(", ", added)}.\n"
                 + "Send a message: ONE call now yields the host's reply plus a reaction from each member who "
                 + "speaks or gestures (others silent). Run cr.council_test off to disarm.";
         }
         catch (Exception ex) { return $"cr.council_test failed: {ex.GetType().Name}: {ex.Message}"; }
      }
   }
}
