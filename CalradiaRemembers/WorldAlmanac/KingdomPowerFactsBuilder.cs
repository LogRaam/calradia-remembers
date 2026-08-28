// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac pillar, the engine seam: gathers the live per-kingdom facts the pure PowerIndexPolicy scores.
// Every field is read defensively (a missing/renamed engine API on one field leaves it 0 rather than losing the
// whole kingdom), because this is the tuning instrument: cr.powerindex dumps the result against a real campaign
// so the raw weights can be tuned before any UI exists. War-record and momentum facts stay 0 here; they need the
// persisted history/battle ledger of a later increment.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.WorldAlmanac
{
   /// <summary>Builds a field of <see cref="KingdomPowerFacts" /> from the live campaign, one per living kingdom.</summary>
   public static class KingdomPowerFactsBuilder
   {
      /// <summary>Gathers facts for every non-eliminated kingdom. Empty when no campaign is running. Never throws.</summary>
      public static List<KingdomPowerFacts> Build()
      {
         var facts = new List<KingdomPowerFacts>();
         if (Campaign.Current == null || Kingdom.All == null) return facts;

         foreach (Kingdom k in Kingdom.All)
         {
            if (k == null || k.IsEliminated) continue;

            KingdomPowerFacts f = null;
            SafeRun.Step($"KingdomPowerFactsBuilder({k.Name})", () => f = BuildOne(k));
            if (f != null) facts.Add(f);
         }

         return facts;
      }

      #region private

      private static KingdomPowerFacts BuildOne(Kingdom k)
      {
         var f = new KingdomPowerFacts {
            KingdomId = k.StringId,
            KingdomName = k.Name?.ToString() ?? k.StringId,
            LeaderName = k.Leader?.Name?.ToString() ?? "",
            LeaderInfluence = (int) (k.RulingClan?.Influence ?? k.Leader?.Clan?.Influence ?? 0f)
         };

         List<Clan> clans = Clan.All?.Where(c => c != null && !c.IsEliminated && c.Kingdom == k).ToList() ?? new List<Clan>();
         f.ClanCount = clans.Count;
         f.CumulativeInfluence = (int) clans.Sum(c => c.Influence);
         f.VassalCount = clans.Sum(c => c.Heroes?.Count(h => h != null && h.IsAlive && h.IsLord) ?? 0);
         f.EstimatedWealth = clans.Sum(c => (long) (c.Heroes?.Where(h => h != null && h.IsAlive && h.IsLord).Sum(h => (long) h.Gold) ?? 0L));

         // Settlements owned by this kingdom, by type, with a prosperity/hearth proxy for the economy pillar.
         List<Settlement> owned = Settlement.All?.Where(s => s != null && s.MapFaction == k).ToList() ?? new List<Settlement>();
         f.Towns = owned.Count(s => s.IsTown);
         f.Castles = owned.Count(s => s.IsCastle);
         f.Villages = owned.Count(s => s.IsVillage);
         f.TotalProsperity = owned.Sum(SettlementProsperity);

         // Field troops: lord parties flying this kingdom's colours. Garrison troops: what stands in its walls.
         f.FieldTroops = MobileParty.All?
                        .Where(p => p != null && p.IsActive && p.IsLordParty && p.MapFaction == k)
                        .Sum(p => p.MemberRoster?.TotalManCount ?? 0) ?? 0;
         f.GarrisonTroops = owned.Sum(s => s.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0);
         f.ArmyCount = k.Armies?.Count ?? 0;

         // Diplomacy: how many wars this kingdom is currently fighting.
         f.ActiveWars = Kingdom.All?.Count(o => o != null && o != k && !o.IsEliminated && k.IsAtWarWith(o)) ?? 0;

         // Placeholders until the persisted layer lands: quality proxy, war record, alliances, momentum.
         f.VeteranRatio = 0.0;

         return f;
      }

      /// <summary>Prosperity for a town/castle, or hearth as the village proxy; 0 when neither is available.</summary>
      private static double SettlementProsperity(Settlement s)
      {
         if (s == null) return 0.0;
         if (s.Town != null) return s.Town.Prosperity;
         if (s.Village != null) return s.Village.Hearth;

         return 0.0;
      }

      #endregion
   }
}
