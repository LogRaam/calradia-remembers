// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac pillar, the tuning instrument. cr.powerindex gathers the live per-kingdom facts and prints the
// scored ranking, pillars, raw facts, key indicators and totals, so the Power Index formula can be validated and
// its weights tuned against a real campaign BEFORE any UI is built. Read-only: it computes and reports, never
// mutates the world.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.WorldAlmanac;
using CalradiaRemembers.WorldAlmanac;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
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
               sb.AppendLine($"     {f.FieldTroops}+{f.GarrisonTroops} troops (str {f.FieldStrength:F0}+{f.GarrisonStrength:F0}), {f.ArmyCount} armies | "
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

      /// <summary>
      ///   cr.balanceofpower
      ///   Opens the "Balance of Power" panel (the World Almanac dashboard) over the current screen. For visual
      ///   iteration for now; the leak-free campaign-bar entry comes later. Read-and-close, so a brief open over
      ///   the map is fine.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("balanceofpower", "cr")]
      public static string BalanceOfPower(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         WorldAlmanac.BalanceOfPowerManager.Open();

         return "Opening the Balance of Power panel.";
      }

      /// <summary>cr.assess_veteran : the companion path with a MASTER-Tactics strategist (fires the real LLM, prints the counsel + logs the prompt).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("assess_veteran", "cr")]
      public static string AssessVeteran(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         string culture = Hero.MainHero?.Culture?.Name?.ToString() ?? "";
         string name = Clan.PlayerClan?.Companions?.FirstOrDefault(h => h != null && h.IsAlive)?.Name?.ToString() ?? "Ira the Strategist";

         return VoicedAssessment(new StrategicAdvisor(name, culture, 260, AdvisorKind.CompanionStrategist));
      }

      /// <summary>cr.assess_soldier : the fallback path with a common soldier (green Tactics; fires the real LLM, prints the counsel + logs the prompt).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("assess_soldier", "cr")]
      public static string AssessSoldier(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         string culture = Hero.MainHero?.Culture?.Name?.ToString() ?? "";

         return VoicedAssessment(new StrategicAdvisor("", culture, 30, AdvisorKind.CommonSoldier));
      }

      /// <summary>cr.assess_alone : the player-alone path, the OFFLINE factual summary with NO LLM call.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("assess_alone", "cr")]
      public static string AssessAlone(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         List<KingdomPowerFacts> field = KingdomPowerFactsBuilder.Build();
         if (field.Count == 0) return "No living kingdoms found.";

         PowerIndexResult r = PowerIndexPolicy.Compute(field);
         PlayerStrategicContext player = StrategicAssessmentContextBuilder.Build(r).Player;
         string summary = StrategicAssessmentSummary.Compose(r, player);

         ModLog.Info("[ASSESS alone]\n" + summary);

         return "=== Strategic Assessment (player alone, offline, NO LLM) ===\n" + summary;
      }

      // Builds the live standings + the real player context, forces the given advisor, fires ONE synchronous LLM
      // call (dev only, briefly blocks the console) and prints the counsel; the full prompt goes to the log so its
      // grounding can be inspected. Falls back to the offline summary when no AI is configured.
      private static string VoicedAssessment(StrategicAdvisor advisor)
      {
         List<KingdomPowerFacts> field = KingdomPowerFactsBuilder.Build();
         if (field.Count == 0) return "No living kingdoms found.";

         PowerIndexResult r = PowerIndexPolicy.Compute(field);
         WorldAlmanac.StrategicAssessmentContext ctx = StrategicAssessmentContextBuilder.Build(r);
         PlayerStrategicContext player = ctx.Player;
         string prompt = StrategicAssessmentPromptBuilder.Build(r, player, advisor, ctx.Wars, CalradiaRemembers.Styles.StyleService.CurrentRules());

         ILlmClient client = NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;
         if (client == null)
            return "No AI is configured. Offline summary instead:\n" + StrategicAssessmentSummary.Compose(r, player);

         var request = new LlmRequest
         {
            SystemPrompt = prompt,
            Messages = new List<LlmMessage> {new LlmMessage(MessageRole.User, "Give your counsel now.")},
            Parameters = new LlmParameters {MaxTokens = 480, Creativity = 0.6f, ReasoningOverride = "off"}
         };

         string counsel;
         try
         {
            LlmResponse resp = Task.Run(() => client.CompleteAsync(request)).GetAwaiter().GetResult();
            counsel = resp is {IsSuccess: true} && !string.IsNullOrWhiteSpace(resp.Content)
               ? resp.Content.Trim()
               : "(the AI returned nothing)";
         }
         catch (Exception ex)
         {
            counsel = $"(AI call failed: {ex.GetType().Name}: {ex.Message})";
         }

         string who = player.HasRealm ? $"{player.PlayerKingdomName} (rank {player.PlayerKingdomRank})" : "landless";
         var sb = new StringBuilder();
         sb.AppendLine($"=== Strategic Assessment ({advisor.Kind}: {advisor.Name}, Tactics {advisor.TacticsSkill} = {advisor.Tier}) ===");
         sb.AppendLine($"Player: {who}, at war with {player.AtWarWith.Count}.");
         sb.AppendLine("--- Counsel ---");
         sb.AppendLine(counsel);

         string report = sb.ToString();
         ModLog.Info("[ASSESS]\n" + report + "\n--- PROMPT ---\n" + prompt);

         return report;
      }

      private static string Name(Dictionary<string, KingdomPowerFacts> byId, string id)
         => id != null && byId.TryGetValue(id, out KingdomPowerFacts f) ? f.KingdomName : id ?? "(none)";
   }
}
