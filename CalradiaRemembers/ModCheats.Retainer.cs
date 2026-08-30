// Code written by Gabriel Mailhot, 11/08/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Retainer;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.retainer [take|release|status|enable] [name|on|off]
      ///   Retainer (ride_with_me): make an other-clan lord/lady ride IN the player's own party for a time,
      ///   WITHOUT marriage and WITHOUT joining the player's clan (their Hero.Clan stays their own), and have a
      ///   war with their faction force them to leave.
      ///   - enable [on|off]: toggle the PLAYER-FACING ride_with_me / part_ways path (RetainerBehavior.Enabled,
      ///                      ON by default via the MCM "Lords can ride in your party" toggle; this sets a
      ///                      session-only override on top of it). With no argument, prints the current state.
      ///   - take [name]:     add the named other-clan lord to MobileParty.MainParty; reports roster/clan/occupation.
      ///   - release [name]:  remove a tracked retainer from the party; reports the resulting partyless state.
      ///   - status:          list every tracked retainer with clan + whether they are ACTUALLY still in MainParty.
      ///   take/release/status drive the raw mechanism directly and work whether or not the path is enabled.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("retainer", "cr")]
      public static string Retainer(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         RetainerBehavior behavior = RetainerBehavior.Instance;
         if (behavior == null) return "Retainer behavior is not active (start a campaign first).";

         string sub = args is {Count: > 0}
            ? args[0].Trim().ToLowerInvariant()
            : "status";

         if (sub == "status") return behavior.DescribeStatus();

         if (sub == "enable")
         {
            string flag = args is {Count: > 1}
               ? args[1].Trim().ToLowerInvariant()
               : null;

            if (flag == "on" || flag == "true") RetainerBehavior.Enabled = true;
            else if (flag == "off" || flag == "false") RetainerBehavior.Enabled = false;

            return $"ride_with_me path is now {(RetainerBehavior.Enabled ? "ENABLED" : "DISABLED")}. Usage: cr.retainer enable [on|off].";
         }

         Hero lord = ResolveHero(args.Skip(1));
         if (lord == null) return "No matching hero. Usage: cr.retainer [take|release|status|enable] [name|on|off].";

         if (sub == "take")
         {
            behavior.TryTake(lord, out string report);

            return report;
         }

         if (sub == "release")
         {
            behavior.TryRelease(lord, out string report);

            return report;
         }

         return "Usage: cr.retainer [take|release|status|enable] [name|on|off].";
      }
   }
}
