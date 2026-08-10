// Code written by Gabriel Mailhot, 10/08/2026.
// Arms/disarms and manually fires the war-council OFFER trigger, which ships behind a kill-switch
// (WarCouncilOfferBehavior.Enabled, off by default) because the council window it opens is still being validated
// in-game. 'now' forces the offer inquiry so the whole convene path can be exercised without staging a real
// army action first.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.WarCouncil;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.warcouncil [on|off|now]
      ///   With no argument, reports whether the offer trigger is armed and its cooldown state. With on/off, flips
      ///   <see cref="WarCouncilOfferBehavior.Enabled" /> FOR THIS SESSION only (a code default, never persisted),
      ///   so the after-an-army-action offer can be play-tested without a rebuild. With 'now', forces the offer
      ///   inquiry immediately (bypassing the in-army / notable / cooldown gates) to exercise the convene path.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("warcouncil", "cr")]
      public static string WarCouncil(List<string> args)
      {
         try
         {
            WarCouncilOfferBehavior behavior = WarCouncilOfferBehavior.Instance;

            if (behavior == null)
               return "War-council offer behavior is not active. Load a campaign first.";

            string arg = args is {Count: > 0} ? args[0].ToLowerInvariant() : null;

            switch (arg)
            {
               case "on":
                  WarCouncilOfferBehavior.Enabled = true;

                  return "War-council offer ARMED for this session. It will offer to convene after your army forms, "
                       + "captures a settlement, or wins a notable battle (once per cooldown). Use 'cr.warcouncil now' "
                       + "to force the offer immediately.";
               case "off":
                  WarCouncilOfferBehavior.Enabled = false;

                  return "War-council offer DISARMED. No offer will surface after army actions.";
               case "now":
                  behavior.ForceOfferNow();

                  return "Forced the war-council offer inquiry (gates bypassed). Choose 'Convene now' to open the council.";
               case null:
                  return behavior.DescribeState() + "\nUse: cr.warcouncil on | off | now.";
               default:
                  return $"Unknown argument '{arg}'. Use: cr.warcouncil [on|off|now].";
            }
         }
         catch (Exception ex) { return $"cr.warcouncil failed: {ex.GetType().Name}: {ex.Message}"; }
      }
   }
}
