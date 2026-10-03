// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac pillar, pure: turns a field of KingdomPowerFacts into the "Balance of Power" dashboard, a
// normalized Power Index per kingdom plus the key indicators and field totals. Six pillars, weighted the way
// Gabriel set them (Military 30, Territory 20, Economy 15, Political 15, War 15, Diplomacy 5), each scored 0..100
// RELATIVE TO THE FIELD (a kingdom's share of the strongest in that pillar), softly capped by a headroom factor
// so the leader lands in the low 90s rather than a flat 100, matching the mockup and leaving room to climb. The
// magnitude pillars (military, territory, economy, political) use share-of-max, so a weaker realm keeps a
// proportional score rather than being zeroed; the balance pillars (war, diplomacy) use a min-max spread that
// falls back to a neutral midpoint when the field has no signal yet (war performance is neutral in V1, before
// the persisted battle ledger exists). Every constant is a tuning knob: cr.powerindex dumps this against a real
// campaign so the weights can be tuned before any UI is built.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
   /// <summary>Computes the Power Index dashboard from a field of kingdom facts.</summary>
   public static class PowerIndexPolicy
   {
      // ── Pillar weights (sum to 1) ─────────────────────────────────────────
      public const double MilitaryWeight = 0.30;
      public const double TerritoryWeight = 0.20;
      public const double EconomyWeight = 0.15;
      public const double PoliticalWeight = 0.15;
      public const double WarWeight = 0.15;
      public const double DiplomacyWeight = 0.05;

      /// <summary>The strongest in a pillar scores 100/Headroom (~93), not a flat 100, so dominance still has somewhere to grow.</summary>
      public const double Headroom = 1.08;

      /// <summary>What every kingdom scores in a pillar the field cannot yet differentiate (e.g. war performance in V1).</summary>
      public const double NeutralScore = 50.0;

      // ── Raw-scalar tuning knobs (relative weights INSIDE a pillar; magnitudes are normalized away) ──
      public const double GarrisonStrengthWeight = 0.5; // garrison strength counts, but a field army projects power (Gabriel: keep 0.5)
      public const double ArmyMusterWeight = 150.0;     // a mustered army is concentrated, coordinated force

      public const double TownTerritoryWeight = 3.0;
      public const double CastleTerritoryWeight = 2.0;
      public const double VillageTerritoryWeight = 1.0;

      public const double WealthEconomyDivisor = 100.0; // denars scaled to sit alongside prosperity (both production AND treasury count)

      // Political weighs the SIZE of the noble bloc first (clans, vassals), with influence only an appoint: a
      // realm of many houses is politically heavy even under a low-influence ruler (tuning 2026-08-27, after the
      // first cr.powerindex showed cumulative influence swamping the count and burying the many-clan realms).
      public const double ClanPoliticalWeight = 15.0;
      public const double VassalPoliticalWeight = 3.0;
      public const double CumulativeInfluenceWeight = 0.02;
      public const double LeaderInfluenceWeight = 0.05;

      public const double SettlementSwingWeight = 2.0; // a captured/lost settlement weighs more than a field win

      /// <summary>
      ///   Scores and ranks the whole field. A null or empty field yields an empty result. The result's Ranked
      ///   list is ordered by Power Index descending with Rank stamped 1..N.
      /// </summary>
      public static PowerIndexResult Compute(IReadOnlyList<KingdomPowerFacts> field)
      {
         var result = new PowerIndexResult();
         if (field == null || field.Count == 0) return result;

         List<KingdomPowerFacts> kingdoms = field.Where(k => k != null).ToList();
         if (kingdoms.Count == 0) return result;

         // Raw scalar per pillar per kingdom (index-aligned with `kingdoms`).
         double[] military = kingdoms.Select(MilitaryRaw).ToArray();
         double[] territory = kingdoms.Select(TerritoryRaw).ToArray();
         double[] economy = kingdoms.Select(EconomyRaw).ToArray();
         double[] political = kingdoms.Select(PoliticalRaw).ToArray();
         double[] war = kingdoms.Select(WarRaw).ToArray();
         double[] diplomacy = kingdoms.Select(DiplomacyRaw).ToArray();

         double milMax = military.Max();
         double terMax = territory.Max();
         double ecoMax = economy.Max();
         double polMax = political.Max();

         var scores = new List<KingdomPowerScore>(kingdoms.Count);
         for (var i = 0; i < kingdoms.Count; i++)
         {
            var s = new KingdomPowerScore {
               KingdomId = kingdoms[i].KingdomId,
               KingdomName = kingdoms[i].KingdomName,
               Military = ShareOfMax(military[i], milMax),
               Territory = ShareOfMax(territory[i], terMax),
               Economy = ShareOfMax(economy[i], ecoMax),
               Political = ShareOfMax(political[i], polMax),
               WarPerformance = SpreadOrNeutral(war[i], war),
               RecentVictories = kingdoms[i].RecentVictories,
               RecentDefeats = kingdoms[i].RecentDefeats,
               Diplomatic = SpreadOrNeutral(diplomacy[i], diplomacy)
            };
            s.PowerIndex = s.Military * MilitaryWeight
                         + s.Territory * TerritoryWeight
                         + s.Economy * EconomyWeight
                         + s.Political * PoliticalWeight
                         + s.WarPerformance * WarWeight
                         + s.Diplomatic * DiplomacyWeight;
            scores.Add(s);
         }

         // Rank by Power Index, strongest first; stamp 1..N.
         List<KingdomPowerScore> ranked = scores.OrderByDescending(s => s.PowerIndex).ToList();
         for (var i = 0; i < ranked.Count; i++) ranked[i].Rank = i + 1;

         result.Ranked = ranked;
         result.Indicators = BuildIndicators(kingdoms);
         result.Totals = BuildTotals(kingdoms);

         return result;
      }

      #region private

      // ── Pillar raw scalars (bigger = stronger; magnitudes are normalized away downstream) ──

      // Tier-weighted fighting strength, not raw headcount (Gabriel: it is military STRENGTH that counts): a
      // veteran host outweighs a larger levy. Field strength full, garrison strength discounted, plus a small
      // muster bonus for coordinated armies.
      private static double MilitaryRaw(KingdomPowerFacts k)
         => k.FieldStrength + k.GarrisonStrength * GarrisonStrengthWeight + k.ArmyCount * ArmyMusterWeight;

      private static double TerritoryRaw(KingdomPowerFacts k)
         => k.Towns * TownTerritoryWeight + k.Castles * CastleTerritoryWeight + k.Villages * VillageTerritoryWeight;

      private static double EconomyRaw(KingdomPowerFacts k)
         => k.TotalProsperity + k.EstimatedWealth / WealthEconomyDivisor;

      private static double PoliticalRaw(KingdomPowerFacts k)
         => k.ClanCount * ClanPoliticalWeight
            + k.VassalCount * VassalPoliticalWeight
            + k.CumulativeInfluence * CumulativeInfluenceWeight
            + k.LeaderInfluence * LeaderInfluenceWeight;

      // War performance can be negative (net defeats); the spread normalizer handles the sign and the all-zero
      // V1 field (no ledger yet) collapses to the neutral midpoint for everyone.
      private static double WarRaw(KingdomPowerFacts k)
         => (k.RecentVictories - k.RecentDefeats)
            + (k.SettlementsCaptured - k.SettlementsLost) * SettlementSwingWeight;

      // Fewer simultaneous wars and more standing alliances is a stronger diplomatic position.
      private static double DiplomacyRaw(KingdomPowerFacts k) => k.Alliances - k.ActiveWars;

      // ── Normalizers ──

      /// <summary>Share of the field's strongest in this pillar, softly capped (leader ~ 100/Headroom). Neutral when the field is empty of this pillar.</summary>
      private static double ShareOfMax(double raw, double max)
      {
         if (max <= 0) return NeutralScore; // nobody has any of this pillar; do not drag the whole field to zero
         double score = 100.0 * raw / (max * Headroom);

         return Clamp(score, 0.0, 100.0);
      }

      /// <summary>Min-max spread across the field (handles negative raws), or the neutral midpoint when the field is flat (no signal).</summary>
      private static double SpreadOrNeutral(double raw, IReadOnlyList<double> all)
      {
         double min = all.Min();
         double max = all.Max();
         if (max <= min) return NeutralScore; // every kingdom equal (e.g. war record all zero in V1)

         double score = 100.0 * (raw - min) / ((max - min) * Headroom);

         return Clamp(score, 0.0, 100.0);
      }

      private static KeyIndicators BuildIndicators(List<KingdomPowerFacts> kingdoms)
      {
         KingdomPowerFacts army = kingdoms.OrderByDescending(k => k.FieldTroops + k.GarrisonTroops).First();
         KingdomPowerFacts land = kingdoms.OrderByDescending(k => k.TotalSettlements).First();
         KingdomPowerFacts wealth = kingdoms.OrderByDescending(k => k.EstimatedWealth).First();
         KingdomPowerFacts leader = kingdoms.OrderByDescending(k => k.LeaderInfluence).First();
         KingdomPowerFacts warRecord = kingdoms.OrderByDescending(k => k.RecentVictories - k.RecentDefeats).First();

         return new KeyIndicators {
            MostPowerfulArmyKingdomId = army.KingdomId,
            MostPowerfulArmyTroops = army.FieldTroops + army.GarrisonTroops,
            LargestTerritoryKingdomId = land.KingdomId,
            LargestTerritorySettlements = land.TotalSettlements,
            StrongestEconomyKingdomId = wealth.KingdomId,
            StrongestEconomyWealth = wealth.EstimatedWealth,
            MostInfluentialLeaderKingdomId = leader.KingdomId,
            MostInfluentialLeaderName = leader.LeaderName,
            MostInfluentialLeaderInfluence = leader.LeaderInfluence,
            BestWarRecordKingdomId = warRecord.KingdomId,
            BestWarRecordVictories = warRecord.RecentVictories,
            BestWarRecordDefeats = warRecord.RecentDefeats
         };
      }

      private static PowerFieldTotals BuildTotals(List<KingdomPowerFacts> kingdoms)
         => new PowerFieldTotals {
            TotalTroops = kingdoms.Sum(k => k.FieldTroops + k.GarrisonTroops),
            TotalSettlements = kingdoms.Sum(k => k.TotalSettlements),
            TotalClans = kingdoms.Sum(k => k.ClanCount),
            TotalWealth = kingdoms.Sum(k => k.EstimatedWealth)
         };

      private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

      #endregion
   }
}
