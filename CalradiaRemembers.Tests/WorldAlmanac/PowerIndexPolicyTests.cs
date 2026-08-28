// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac, the Power Index. This policy IS the dashboard's brain: the whole "Balance of Power" screen is
// just a rendering of what it returns, and Gabriel tunes the weights against a real campaign through
// cr.powerindex before any UI is built, so the numbers have to behave predictably. What matters is not the exact
// magic constants (those are knobs) but the SHAPE: more of a pillar scores higher, the field leader sits in the
// low 90s rather than a flat 100 (the headroom that lets dominance still grow), a pillar the field cannot yet
// tell apart (war record in V1, before the battle ledger) collapses to a neutral midpoint instead of dragging
// everyone to zero, the ranking follows the index, and the key indicators point at the true single-measure
// leaders. Get the normalization wrong and the panel either crowns everyone equally or zeroes the weak.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.WorldAlmanac;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.WorldAlmanac
{
   [TestFixture]
   public sealed class PowerIndexPolicyTests
   {
      // The strongest in a pillar sets the ceiling and everyone else is a proportional share of it: a weaker
      // realm keeps a real, mid-range score rather than being zeroed, which is what keeps the balance-of-power
      // list readable down to sixth place instead of a lone giant and a row of noughts.
      [Test]
      public void GIVEN_two_kingdoms_of_different_strength_WHEN_scoring_THEN_the_leader_tops_the_pillar_and_the_other_is_a_proportional_share()
      {
         var field = new List<KingdomPowerFacts> {K("A", fieldStrength: 1000), K("B", fieldStrength: 500)};

         PowerIndexResult r = PowerIndexPolicy.Compute(field);

         KingdomPowerScore a = Find(r, "A");
         KingdomPowerScore b = Find(r, "B");
         a.Military.Should().BeApproximately(100.0 / PowerIndexPolicy.Headroom, 0.5); // leader ~93, not a flat 100
         b.Military.Should().BeApproximately(a.Military / 2.0, 0.5);                  // half the strength, ~half the score
      }

      // The headroom is the whole point of option (a): the top kingdom must NOT read 100, so a realm that later
      // pulls further ahead still has somewhere to climb on the bar.
      [Test]
      public void GIVEN_a_clear_leader_WHEN_scoring_THEN_its_pillar_stays_below_one_hundred()
      {
         var field = new List<KingdomPowerFacts> {K("A", towns: 10), K("B", towns: 2)};

         Find(PowerIndexPolicy.Compute(field), "A").Territory.Should().BeLessThan(100.0).And.BeGreaterThan(85.0);
      }

      // The index is the weighted blend and the list comes back ordered by it, ranks stamped 1..N: this is the
      // podium order the panel draws, so a mis-sort would crown the wrong kingdom.
      [Test]
      public void GIVEN_a_field_WHEN_scoring_THEN_it_is_ranked_by_power_index_descending()
      {
         var field = new List<KingdomPowerFacts> {
            K("Weak", fieldStrength: 200, towns: 1),
            K("Strong", fieldStrength: 2000, towns: 10, castles: 8),
            K("Middle", fieldStrength: 900, towns: 4, castles: 3)
         };

         PowerIndexResult r = PowerIndexPolicy.Compute(field);

         r.Ranked[0].KingdomId.Should().Be("Strong");
         r.Ranked[0].Rank.Should().Be(1);
         r.Ranked[2].KingdomId.Should().Be("Weak");
         r.Ranked[2].Rank.Should().Be(3);
         r.Ranked[0].PowerIndex.Should().BeGreaterThan(r.Ranked[1].PowerIndex);
         r.Ranked[1].PowerIndex.Should().BeGreaterThan(r.Ranked[2].PowerIndex);
      }

      // The six weights must sum to one, or the Power Index would not sit on the same 0..100 scale as the pillars
      // and the number under a kingdom's name would drift off the bar it is drawn against.
      [Test]
      public void GIVEN_the_pillar_weights_WHEN_summed_THEN_they_make_a_whole()
      {
         double sum = PowerIndexPolicy.MilitaryWeight + PowerIndexPolicy.TerritoryWeight
                    + PowerIndexPolicy.EconomyWeight + PowerIndexPolicy.PoliticalWeight
                    + PowerIndexPolicy.WarWeight + PowerIndexPolicy.DiplomacyWeight;

         sum.Should().BeApproximately(1.0, 1e-9);
      }

      // V1 has no battle ledger, so every kingdom's war record is zero. A flat pillar must resolve to the neutral
      // midpoint for all, NOT zero: otherwise war performance (15% of the index) would silently subtract the same
      // chunk from everyone and the indices would read low across the board.
      [Test]
      public void GIVEN_no_war_data_in_the_field_WHEN_scoring_THEN_every_war_pillar_is_neutral()
      {
         var field = new List<KingdomPowerFacts> {K("A", fieldTroops: 1000), K("B", fieldTroops: 500)};

         PowerIndexResult r = PowerIndexPolicy.Compute(field);

         Find(r, "A").WarPerformance.Should().Be(PowerIndexPolicy.NeutralScore);
         Find(r, "B").WarPerformance.Should().Be(PowerIndexPolicy.NeutralScore);
      }

      // Once there IS a war signal, the record spreads across the field and can be negative: a realm on a losing
      // streak scores near the floor, a conquering one near the top. This is the momentum the dashboard exists to
      // surface, so the sign must not be swallowed.
      [Test]
      public void GIVEN_a_real_war_record_WHEN_scoring_THEN_the_victor_beats_the_loser_on_the_war_pillar()
      {
         var field = new List<KingdomPowerFacts> {
            K("Victor", fieldTroops: 1000, wins: 10, losses: 0),
            K("Loser", fieldTroops: 1000, wins: 0, losses: 10)
         };

         PowerIndexResult r = PowerIndexPolicy.Compute(field);

         Find(r, "Victor").WarPerformance.Should().BeGreaterThan(Find(r, "Loser").WarPerformance);
         Find(r, "Loser").WarPerformance.Should().BeApproximately(0.0, 0.5); // the field's worst sits at the floor
      }

      // The Key Indicators panel calls out single-measure leaders, each a plain argmax over the raw facts. They
      // are independent of the blended index (the biggest army need not be the strongest realm), so each is pinned
      // to the kingdom that actually tops that one measure.
      [Test]
      public void GIVEN_a_field_WHEN_scoring_THEN_the_key_indicators_point_at_each_single_measure_leader()
      {
         var field = new List<KingdomPowerFacts> {
            K("Army", fieldTroops: 5000, garrison: 1000, towns: 1, wealth: 1_000, leaderInfluence: 100, leader: "Warlord"),
            K("Land", fieldTroops: 100, towns: 12, castles: 10, villages: 30, wealth: 2_000, leaderInfluence: 200, leader: "Steward"),
            K("Rich", fieldTroops: 100, towns: 2, wealth: 9_000_000, leaderInfluence: 300, leader: "Merchant"),
            K("Court", fieldTroops: 100, towns: 2, wealth: 1_000, leaderInfluence: 5_000, leader: "Rhagaea", wins: 20, losses: 1)
         };

         KeyIndicators k = PowerIndexPolicy.Compute(field).Indicators;

         k.MostPowerfulArmyKingdomId.Should().Be("Army");
         k.MostPowerfulArmyTroops.Should().Be(6000);
         k.LargestTerritoryKingdomId.Should().Be("Land");
         k.LargestTerritorySettlements.Should().Be(52);
         k.StrongestEconomyKingdomId.Should().Be("Rich");
         k.MostInfluentialLeaderKingdomId.Should().Be("Court");
         k.MostInfluentialLeaderName.Should().Be("Rhagaea");
         k.BestWarRecordKingdomId.Should().Be("Court");
      }

      // The Calradia Overview strip is field-wide sums; a miscount here would misreport the whole realm's totals
      // under an otherwise correct board.
      [Test]
      public void GIVEN_a_field_WHEN_scoring_THEN_the_totals_sum_across_all_kingdoms()
      {
         var field = new List<KingdomPowerFacts> {
            K("A", fieldTroops: 1000, garrison: 200, towns: 3, castles: 1, villages: 6, clans: 5, wealth: 1_000_000),
            K("B", fieldTroops: 500, garrison: 100, towns: 2, castles: 2, villages: 4, clans: 3, wealth: 500_000)
         };

         PowerFieldTotals t = PowerIndexPolicy.Compute(field).Totals;

         t.TotalTroops.Should().Be(1800);        // 1000+200+500+100
         t.TotalSettlements.Should().Be(18);      // (3+1+6)+(2+2+4)
         t.TotalClans.Should().Be(8);
         t.TotalWealth.Should().Be(1_500_000);
      }

      // Every index must stay on the 0..100 scale the bars are drawn on, whatever the spread of raw facts.
      [Test]
      public void GIVEN_any_field_WHEN_scoring_THEN_every_power_index_is_within_zero_and_one_hundred()
      {
         var field = new List<KingdomPowerFacts> {
            K("Huge", fieldTroops: 50_000, towns: 20, wealth: 20_000_000, clans: 40, leaderInfluence: 9000),
            K("Tiny", fieldTroops: 1, towns: 0, villages: 1)
         };

         foreach (KingdomPowerScore s in PowerIndexPolicy.Compute(field).Ranked)
            s.PowerIndex.Should().BeInRange(0.0, 100.0);
      }

      // A single kingdom is trivially the whole field: it ranks first and tops its pillars at the headroom
      // ceiling, so a one-kingdom world (or a debug call) never divides by zero or crowns nobody.
      [Test]
      public void GIVEN_a_single_kingdom_WHEN_scoring_THEN_it_ranks_first_at_the_headroom_ceiling()
      {
         PowerIndexResult r = PowerIndexPolicy.Compute(new List<KingdomPowerFacts> {K("Lone", fieldStrength: 1000, towns: 5)});

         r.Ranked.Should().HaveCount(1);
         r.Ranked[0].Rank.Should().Be(1);
         r.Ranked[0].Military.Should().BeApproximately(100.0 / PowerIndexPolicy.Headroom, 0.5);
      }

      // A null or empty field yields an empty result, never a crash: a call before any kingdom exists (very early
      // campaign, a debug command) must degrade quietly.
      [Test]
      public void GIVEN_no_field_WHEN_scoring_THEN_the_result_is_empty()
      {
         PowerIndexPolicy.Compute(null).Ranked.Should().BeEmpty();
         PowerIndexPolicy.Compute(new List<KingdomPowerFacts>()).Ranked.Should().BeEmpty();
      }

      #region private

      private static KingdomPowerScore Find(PowerIndexResult r, string id)
      {
         foreach (KingdomPowerScore s in r.Ranked)
            if (s.KingdomId == id)
               return s;

         return null;
      }

      private static KingdomPowerFacts K(
         string id, int fieldTroops = 0, int garrison = 0, int armies = 0, double fieldStrength = 0, double garrisonStrength = 0,
         int towns = 0, int castles = 0, int villages = 0, double prosperity = 0, long wealth = 0,
         int clans = 0, int vassals = 0, int cumInfluence = 0, int leaderInfluence = 0, string leader = null,
         int wins = 0, int losses = 0, int captured = 0, int lost = 0, int wars = 0, int alliances = 0)
         => new KingdomPowerFacts {
            KingdomId = id,
            KingdomName = id,
            LeaderName = leader ?? id,
            FieldTroops = fieldTroops,
            GarrisonTroops = garrison,
            ArmyCount = armies,
            FieldStrength = fieldStrength,
            GarrisonStrength = garrisonStrength,
            Towns = towns,
            Castles = castles,
            Villages = villages,
            TotalProsperity = prosperity,
            EstimatedWealth = wealth,
            ClanCount = clans,
            VassalCount = vassals,
            CumulativeInfluence = cumInfluence,
            LeaderInfluence = leaderInfluence,
            RecentVictories = wins,
            RecentDefeats = losses,
            SettlementsCaptured = captured,
            SettlementsLost = lost,
            ActiveWars = wars,
            Alliances = alliances
         };

      #endregion
   }
}
