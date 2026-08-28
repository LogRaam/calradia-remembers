// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac pillar, the tuning instrument. cr.powerindex gathers the live per-kingdom facts and prints the
// scored ranking, pillars, raw facts, key indicators and totals, so the Power Index formula can be validated and
// its weights tuned against a real campaign BEFORE any UI is built. Read-only: it computes and reports, never
// mutates the world.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.WorldAlmanac;
using CalradiaRemembers.WorldAlmanac;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.powerindex
      ///   Prints the "Balance of Power" Power Index for every living kingdom: the scored ranking with its six
      ///   pillars, the raw facts each score is drawn from, the key indicators, and the field totals. Use it to
      ///   sanity-check and tune the formula (in PowerIndexPolicy) against a real campaign before the UI exists.
      ///   The full report is also written to the log file.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("powerindex", "cr")]
      public static string PowerIndex(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         List<KingdomPowerFacts> field = KingdomPowerFactsBuilder.Build();
         if (field.Count == 0) return "No living kingdoms found.";

         PowerIndexResult r = PowerIndexPolicy.Compute(field);
         var byId = field.ToDictionary(f => f.KingdomId);

         var sb = new StringBuilder();
         sb.AppendLine("=== Calradia Power Index (Balance of Power) ===");

         foreach (KingdomPowerScore s in r.Ranked)
         {
            byId.TryGetValue(s.KingdomId, out KingdomPowerFacts f);
            sb.AppendLine($"#{s.Rank}  {s.KingdomName}   Power {s.PowerIndex:F1}");
            sb.AppendLine($"     Mil {s.Military:F0}  Terr {s.Territory:F0}  Eco {s.Economy:F0}  Pol {s.Political:F0}  War {s.WarPerformance:F0}  Dip {s.Diplomatic:F0}");
            if (f != null)
               sb.AppendLine($"     {f.FieldTroops}+{f.GarrisonTroops} troops, {f.ArmyCount} armies | "
                           + $"{f.Towns}T/{f.Castles}C/{f.Villages}V (prosp {f.TotalProsperity:F0}) | "
                           + $"{f.ClanCount} clans, {f.VassalCount} lords | {f.EstimatedWealth:N0} denars | "
                           + $"{f.LeaderName} inf {f.LeaderInfluence} | wars {f.ActiveWars}");
         }

         KeyIndicators ki = r.Indicators;
         sb.AppendLine("--- Key Indicators ---");
         sb.AppendLine($"  Most Powerful Army : {Name(byId, ki.MostPowerfulArmyKingdomId)} ({ki.MostPowerfulArmyTroops:N0})");
         sb.AppendLine($"  Largest Territory  : {Name(byId, ki.LargestTerritoryKingdomId)} ({ki.LargestTerritorySettlements} settlements)");
         sb.AppendLine($"  Strongest Economy  : {Name(byId, ki.StrongestEconomyKingdomId)} ({ki.StrongestEconomyWealth:N0} denars)");
         sb.AppendLine($"  Most Influential   : {ki.MostInfluentialLeaderName} of {Name(byId, ki.MostInfluentialLeaderKingdomId)} (inf {ki.MostInfluentialLeaderInfluence})");
         sb.AppendLine($"  Best War Record    : {Name(byId, ki.BestWarRecordKingdomId)} ({ki.BestWarRecordVictories}W / {ki.BestWarRecordDefeats}L)");

         PowerFieldTotals t = r.Totals;
         sb.AppendLine("--- Calradia Overview ---");
         sb.AppendLine($"  {t.TotalTroops:N0} total troops · {t.TotalSettlements} settlements · {t.TotalClans} clans · {t.TotalWealth:N0} denars");
         sb.AppendLine("(War performance is neutral for now: it needs the persisted battle ledger, a later increment.)");

         string report = sb.ToString();
         ModLog.Info("[POWERINDEX]\n" + report);

         return report;
      }

      private static string Name(Dictionary<string, KingdomPowerFacts> byId, string id)
         => id != null && byId.TryGetValue(id, out KingdomPowerFacts f) ? f.KingdomName : id ?? "(none)";
   }
}
