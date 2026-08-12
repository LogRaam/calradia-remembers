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
      ///   cr.retainer [take|release|status] [name]
      ///   Retainer feasibility spike: make an other-clan lord/lady ride IN the player's own party for a time,
      ///   WITHOUT marriage and WITHOUT joining the player's clan (their Hero.Clan stays their own), and have a
      ///   war with their faction force them to leave. NOT a player feature - a console-gated probe.
      ///   - take [name]:    add the named other-clan lord to MobileParty.MainParty; reports roster/clan/occupation.
      ///   - release [name]: remove a tracked retainer from the party; reports the resulting partyless state.
      ///   - status:         list every tracked retainer with clan + whether they are ACTUALLY still in MainParty.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("retainer", "cr")]
      public static string Retainer(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         RetainerSpikeBehavior behavior = RetainerSpikeBehavior.Instance;
         if (behavior == null) return "Retainer spike behavior is not active (start a campaign first).";

         string sub = args is {Count: > 0}
            ? args[0].Trim().ToLowerInvariant()
            : "status";

         if (sub == "status") return behavior.DescribeStatus();

         Hero lord = ResolveHero(args.Skip(1));
         if (lord == null) return "No matching hero. Usage: cr.retainer [take|release|status] [name].";

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

         return "Usage: cr.retainer [take|release|status] [name].";
      }
   }
}
