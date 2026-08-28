// Code written by Gabriel Mailhot, 27/08/2026.
// World Almanac pillar, the engine seam: gathers the live per-kingdom facts the pure PowerIndexPolicy scores.
// Every field is read defensively (a missing/renamed engine API on one field leaves it 0 rather than losing the
// whole kingdom), because this is the tuning instrument: cr.powerindex dumps the result against a real campaign
// so the raw weights can be tuned before any UI exists. War-record and momentum facts stay 0 here; they need the
// persisted history/battle ledger of a later increment.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Logic.WorldAlmanac;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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

         // Field forces: lord parties flying this kingdom's colours. Both the headcount (for display) and the
         // tier-weighted strength (what the Military pillar actually scores from, Gabriel's call).
         List<MobileParty> lordParties = MobileParty.All?
                                        .Where(p => p != null && p.IsActive && p.IsLordParty && p.MapFaction == k)
                                        .ToList() ?? new List<MobileParty>();
         f.FieldTroops = lordParties.Sum(p => p.MemberRoster?.TotalManCount ?? 0);
         f.FieldStrength = lordParties.Sum(p => PartyStrength(p));

         // Garrisons: what stands in the walls, again both headcount and strength.
         f.GarrisonTroops = owned.Sum(s => s.Town?.GarrisonParty?.MemberRoster?.TotalManCount ?? 0);
         f.GarrisonStrength = owned.Sum(s => PartyStrength(s.Town?.GarrisonParty));

         f.ArmyCount = k.Armies?.Count ?? 0;

         // Diplomacy: how many wars this kingdom is currently fighting.
         f.ActiveWars = Kingdom.All?.Count(o => o != null && o != k && !o.IsEliminated && k.IsAtWarWith(o)) ?? 0;

         // War record and alliances stay 0 until the persisted battle ledger / momentum layer lands.

         return f;
      }

      /// <summary>
      ///   Tier-weighted fighting strength of a party: each troop counts as its own tier (an elite tier-6 counts
      ///   ~6x a raw recruit), so a veteran host outweighs a larger levy. No engine strength API is stable across
      ///   builds, so this is computed from the roster (same GetTroopRoster/Tier read the troop bucketer uses).
      /// </summary>
      private static double PartyStrength(MobileParty p)
      {
         TroopRoster roster = p?.MemberRoster;
         if (roster == null) return 0.0;

         var strength = 0.0;
         foreach (TroopRosterElement el in roster.GetTroopRoster())
         {
            CharacterObject c = el.Character;
            if (c == null) continue;

            strength += el.Number * Math.Max(1, c.Tier);
         }

         return strength;
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
