// Code written by Gabriel Mailhot, 15/10/2026.
// cr.war_duty: what the vassal war duty sees today (Maifailun, Gabriel, 15/10/2026). Read-only.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.WarDuty;
using CalradiaRemembers.WarDuty;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.war_duty: the oath, the realm's wars, the last service, the verdict today and the reckoning in progress.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("war_duty", "cr")]
      public static string WarDutyStatus(List<string> args)
      {
         WarDutyBehavior behavior = WarDutyBehavior.Instance;
         if (behavior == null) return "cr.war_duty: no campaign loaded.";

         int today = WarDutyBehavior.Today;
         WarServiceLedger l = behavior.Ledger;
         WarDutyFacts facts = l.Facts(today, WarDutyBehavior.Live());
         WarDutyCase c = l.Case;

         string wars = l.WarSeenSince.Count == 0 ? "none" : string.Join(", ", l.WarSeenSince.Select(w => $"{w.Key} since day {w.Value}"));
         string reckoning = c == null
            ? "none"
            : $"{c.Stage}, ruler {c.RulerId}, opened day {c.OpenedOnDay}, deadline day {c.DeadlineDay}, levy {c.LevyPaid}/{c.LevyAmount}, appeared {c.Appeared}";

         return $"cr.war_duty (MCM {(ModSettings.EnableWarDuty ? "ON" : "OFF")}, window {ModSettings.WarDutyWindowDays} days, today day {today}):\n"
              + $"  sworn to: {l.SwornKingdomId ?? "none"} since day {l.SwornSinceDay}\n"
              + $"  wars: {wars}\n"
              + $"  last served: {(l.LastServedDay.HasValue ? "day " + l.LastServedDay.Value : "never seen")}; last called: {(l.LastCalledDay.HasValue ? "day " + l.LastCalledDay.Value : "never")}\n"
              + $"  today: {WarDutyPolicy.Decide(facts)}\n"
              + $"  reckoning: {reckoning}";
      }
   }
}
