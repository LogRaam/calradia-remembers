// Code written by Gabriel Mailhot, 04/09/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
    /// <summary>
    ///   The Strategic Assessment when the player travels ALONE (no companion, no soldiers): a plain, factual
    ///   summary of the standings, composed WITHOUT an LLM call. Pure and deterministic so it needs no network,
    ///   never invents, and is unit-tested. It states the facts only, in a neutral voice, with no counsel or
    ///   opinion, since there is no advisor to give one; the companion/soldier paths (LLM prose) are the voiced
    ///   version.
    /// </summary>
    public static class StrategicAssessmentSummary
    {
        public static string Compose(PowerIndexResult result, PlayerStrategicContext player)
        {
            result ??= new PowerIndexResult();
            IReadOnlyList<KingdomPowerScore> ranked = result.Ranked ?? new List<KingdomPowerScore>();

            var sb = new StringBuilder();

            if (ranked.Count == 0)
            {
                sb.Append("Calradia is without a reigning power; no realm holds the field.");
                AppendPlayerLine(sb, player, 0);

                return sb.ToString();
            }

            KingdomPowerScore top = ranked[0];
            sb.Append($"{NameOr(top?.KingdomName)} leads Calradia with a power index of {Round(top?.PowerIndex ?? 0)}.");

            var followers = new List<string>();
            for (var i = 1; i < ranked.Count && followers.Count < 2; i++)
                if (ranked[i] != null) followers.Add(NameOr(ranked[i].KingdomName));
            if (followers.Count > 0)
                sb.Append($" {string.Join(" and ", followers)} follow.");

            AppendIndicators(sb, result.Indicators);
            AppendPlayerLine(sb, player, ranked.Count);

            return sb.ToString();
        }

        #region private

        private static void AppendIndicators(StringBuilder sb, KeyIndicators ind)
        {
            if (ind == null) return;

            var parts = new List<string>();
            if (ind.MostPowerfulArmyTroops > 0) parts.Add($"the strongest army musters {ind.MostPowerfulArmyTroops:N0} soldiers");
            if (ind.LargestTerritorySettlements > 0) parts.Add($"the largest realm holds {ind.LargestTerritorySettlements} settlements");
            if (ind.StrongestEconomyWealth > 0) parts.Add($"the richest treasury nears {ind.StrongestEconomyWealth:N0} denars");

            if (parts.Count > 0) sb.Append($" Elsewhere, {string.Join(", ", parts)}.");
        }

        private static void AppendPlayerLine(StringBuilder sb, PlayerStrategicContext player, int fieldSize)
        {
            if (player == null) return;

            if (player.HasRealm)
            {
                string rank = player.PlayerKingdomRank > 0
                    ? $", standing {player.PlayerKingdomRank}{(fieldSize > 0 ? $" of {fieldSize}" : "")}"
                    : "";
                sb.Append($" Your own realm, {player.PlayerKingdomName}{rank}.");

                if (player.AtWarWith != null && player.AtWarWith.Count > 0)
                    sb.Append($" At war with {string.Join(", ", player.AtWarWith)}.");
            }
        }

        private static string NameOr(string name) => string.IsNullOrWhiteSpace(name) ? "an unnamed realm" : name.Trim();

        private static int Round(double v) => (int) Math.Round(v);

        #endregion
    }
}
