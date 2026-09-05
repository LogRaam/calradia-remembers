// Code written by Gabriel Mailhot, 04/09/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
    /// <summary>Who is voicing the assessment: a trained companion strategist, or a common soldier standing in when no companion is at hand.</summary>
    public enum AdvisorKind
    {
        CompanionStrategist,
        CommonSoldier
    }

    /// <summary>The voice of the Strategic Assessment: their name, culture, Tactics skill (which sets the counsel's quality), and whether they are a trained strategist or a plain soldier.</summary>
    public sealed class StrategicAdvisor
    {
        public StrategicAdvisor(string name, string culture, int tacticsSkill, AdvisorKind kind = AdvisorKind.CompanionStrategist)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? kind == AdvisorKind.CommonSoldier ? "a grizzled veteran of your warband" : "your war council"
                : name.Trim();
            Culture = culture?.Trim() ?? "";
            TacticsSkill = tacticsSkill;
            Kind = kind;
        }

        public string Name { get; }
        public string Culture { get; }
        public int TacticsSkill { get; }
        public AdvisorKind Kind { get; }

        public AdvisorTacticsTier Tier => TacticsAdvicePolicy.TierFor(TacticsSkill);
    }

    /// <summary>The player's own standing, so the advisor counsels FOR the player's realm rather than describing the world neutrally.</summary>
    public sealed class PlayerStrategicContext
    {
        public PlayerStrategicContext(string playerKingdomName, int playerKingdomRank, IReadOnlyList<string> atWarWith)
        {
            PlayerKingdomName = playerKingdomName?.Trim() ?? "";
            PlayerKingdomRank = playerKingdomRank;
            AtWarWith = atWarWith ?? new List<string>();
        }

        /// <summary>The kingdom the player belongs to, or empty when the player is landless / holds no realm.</summary>
        public string PlayerKingdomName { get; }

        /// <summary>1-based rank of the player's kingdom in the field, or 0 when they hold no realm.</summary>
        public int PlayerKingdomRank { get; }

        /// <summary>Names of the kingdoms the player's realm is at war with (may be empty).</summary>
        public IReadOnlyList<string> AtWarWith { get; }

        public bool HasRealm => !string.IsNullOrEmpty(PlayerKingdomName);
    }

    /// <summary>
    ///   Builds the one-shot LLM prompt for the World Almanac's Strategic Assessment: a companion strategist counsels
    ///   the player on how the realms stand, what is likely to come, and what to do. Pure so the load-bearing parts
    ///   are unit-tested: the advisor persona is present, the counsel quality scales with Tactics, the facts are the
    ///   REAL computed standings, the player's own position frames the advice, and the model is told never to invent
    ///   a fact. The engine seam (pick the advisor, gather the player's wars, fire the async call) is a later
    ///   increment; this only shapes the text.
    /// </summary>
    public static class StrategicAssessmentPromptBuilder
    {
        /// <summary>How many kingdoms to lay before the advisor: enough to judge the field, not so many the prompt bloats.</summary>
        public const int MaxKingdomsListed = 8;

        public static string Build(PowerIndexResult result, PlayerStrategicContext player, StrategicAdvisor advisor)
        {
            result ??= new PowerIndexResult();
            player ??= new PlayerStrategicContext("", 0, null);
            advisor ??= new StrategicAdvisor("", "", 0);

            var sb = new StringBuilder();

            // Persona + the quality dial: WHO is speaking and how keen their counsel is. A companion is a trained
            // strategist; a common soldier stands in when no companion is at hand, and speaks plainly, without a
            // strategist's polish. Either way the Tactics tier sets how keen the counsel is.
            if (advisor.Kind == AdvisorKind.CommonSoldier)
            {
                sb.Append($"You are {advisor.Name}, a common soldier in your lord's warband, no trained strategist, but you have marched and fought across Calradia. Speak plainly, as a soldier would, not with an officer's polish. ");
            }
            else
            {
                string cultureClause = string.IsNullOrEmpty(advisor.Culture) ? "" : $" a {advisor.Culture} officer,";
                sb.Append($"You are {advisor.Name},{cultureClause} the strategist counselling your lord. You are {TacticsAdvicePolicy.LabelFor(advisor.Tier)}. ");
            }

            sb.Append(TacticsAdvicePolicy.GuidanceFor(advisor.Tier));
            sb.AppendLine();
            sb.AppendLine();

            // Grounded facts: the REAL standings. Everything the advisor may cite lives here.
            sb.AppendLine("THE STATE OF CALRADIA (verified; cite ONLY what is written here, never invent a number, battle, or event):");
            AppendStandings(sb, result);
            AppendKeyIndicators(sb, result.Indicators);
            AppendTotals(sb, result.Totals);
            sb.AppendLine();

            // The player's own position, so the counsel is FOR them.
            sb.AppendLine("YOUR LORD'S POSITION:");
            AppendPlayerPosition(sb, player, result);
            sb.AppendLine();

            // The task: describe, predict, advise, in character, grounded.
            sb.Append("Counsel your lord directly, in the second person and in character, in three to five sentences: describe how the realms stand, judge what is most likely to come, and advise what your lord should do. ");
            sb.Append("Your reading and your predictions are YOUR judgment, not fact: be as incisive as your skill allows and no more. ");
            sb.Append("Never state a number, war, or event that is not written above.");

            return sb.ToString();
        }

        #region private

        private static void AppendStandings(StringBuilder sb, PowerIndexResult result)
        {
            IReadOnlyList<KingdomPowerScore> ranked = result.Ranked ?? new List<KingdomPowerScore>();
            if (ranked.Count == 0)
            {
                sb.AppendLine("- No kingdom holds the field; Calradia is without a reigning power.");

                return;
            }

            var shown = 0;
            foreach (KingdomPowerScore k in ranked)
            {
                if (k == null) continue;
                if (shown >= MaxKingdomsListed) break;

                (string highName, double highVal) = TopPillar(k);
                (string lowName, double lowVal) = BottomPillar(k);
                sb.AppendLine(
                    $"- {k.Rank}. {NameOr(k.KingdomName)}: power {Round(k.PowerIndex)}. " +
                    $"Strongest in {highName} ({Round(highVal)}), weakest in {lowName} ({Round(lowVal)}).");
                shown++;
            }
        }

        private static void AppendKeyIndicators(StringBuilder sb, KeyIndicators ind)
        {
            if (ind == null) return;

            var parts = new List<string>();
            if (ind.MostPowerfulArmyTroops > 0) parts.Add($"largest army holds {ind.MostPowerfulArmyTroops:N0} soldiers");
            if (ind.LargestTerritorySettlements > 0) parts.Add($"largest realm holds {ind.LargestTerritorySettlements} settlements");
            if (ind.StrongestEconomyWealth > 0) parts.Add($"richest treasury near {ind.StrongestEconomyWealth:N0} denars");
            if (!string.IsNullOrWhiteSpace(ind.MostInfluentialLeaderName))
                parts.Add($"most influential ruler is {ind.MostInfluentialLeaderName} ({ind.MostInfluentialLeaderInfluence} influence)");
            if (ind.BestWarRecordVictories > 0 || ind.BestWarRecordDefeats > 0)
                parts.Add($"best war record stands at {ind.BestWarRecordVictories}-{ind.BestWarRecordDefeats}");

            if (parts.Count > 0) sb.AppendLine($"- At a glance: {string.Join("; ", parts)}.");
        }

        private static void AppendTotals(StringBuilder sb, PowerFieldTotals totals)
        {
            if (totals == null) return;
            if (totals.TotalTroops <= 0 && totals.TotalSettlements <= 0) return;

            sb.AppendLine(
                $"- Across all realms: {totals.TotalTroops:N0} soldiers, {totals.TotalSettlements} settlements, " +
                $"{totals.TotalClans} clans.");
        }

        private static void AppendPlayerPosition(StringBuilder sb, PlayerStrategicContext player, PowerIndexResult result)
        {
            int fieldSize = result.Ranked?.Count ?? 0;

            if (player.HasRealm)
            {
                string rankClause = player.PlayerKingdomRank > 0
                    ? $", standing {player.PlayerKingdomRank}{(fieldSize > 0 ? $" of {fieldSize}" : "")} among the powers"
                    : "";
                sb.AppendLine($"- Your lord's realm is {player.PlayerKingdomName}{rankClause}.");
            }
            else
            {
                sb.AppendLine("- Your lord holds no realm of their own; you counsel a landless captain whose fortunes turn on whom they serve and fight.");
            }

            if (player.AtWarWith != null && player.AtWarWith.Count > 0)
                sb.AppendLine($"- At war with: {string.Join(", ", player.AtWarWith)}.");
            else if (player.HasRealm)
                sb.AppendLine("- Presently at war with no one.");
        }

        // The kingdom's single strongest / weakest pillar, named, so the advisor can speak of "strong armies" or a
        // "brittle economy" without the raw pillar table.
        private static (string name, double value) TopPillar(KingdomPowerScore k)
        {
            (string, double)[] pillars = Pillars(k);
            (string name, double value) best = pillars[0];
            foreach ((string name, double value) p in pillars)
                if (p.value > best.value) best = p;

            return best;
        }

        private static (string name, double value) BottomPillar(KingdomPowerScore k)
        {
            (string, double)[] pillars = Pillars(k);
            (string name, double value) worst = pillars[0];
            foreach ((string name, double value) p in pillars)
                if (p.value < worst.value) worst = p;

            return worst;
        }

        private static (string, double)[] Pillars(KingdomPowerScore k) => new[]
        {
            ("military", k.Military),
            ("territory", k.Territory),
            ("economy", k.Economy),
            ("political weight", k.Political),
            ("war performance", k.WarPerformance),
            ("diplomacy", k.Diplomatic)
        };

        private static string NameOr(string name) => string.IsNullOrWhiteSpace(name) ? "an unnamed realm" : name.Trim();

        private static int Round(double v) => (int) Math.Round(v);

        #endregion
    }
}
