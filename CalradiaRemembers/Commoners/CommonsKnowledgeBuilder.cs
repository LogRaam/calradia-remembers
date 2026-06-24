// Code written by Gabriel Mailhot, 19/06/2026.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Commoners
{
   /// <summary>
   ///   Builds a <see cref="CommonsKnowledge" /> snapshot from a settlement's
   ///   live game state: holder, kingdom, prosperity, security, wars, and
   ///   visiting lords. Used to populate the commoner's slim system prompt.
   /// </summary>
   public static class CommonsKnowledgeBuilder
   {
      /// <summary>
      ///   Builds a <see cref="CommonsKnowledge" /> for the given settlement.
      ///   Returns null only when <paramref name="settlement" /> is null.
      /// </summary>
      public static CommonsKnowledge Build(Settlement settlement)
      {
         if (settlement == null) return null;

         try
         {
            Kingdom kingdom = settlement.OwnerClan?.Kingdom;
            var kingdomName = kingdom?.Name?.ToString();
            var holderName = settlement.OwnerClan?.Leader?.Name?.ToString();
            string culture = settlement.Culture?.Name?.ToString() ?? "Calradian";

            string type = settlement.IsTown ? "town"
               : settlement.IsCastle ? "castle"
               : "village";

            var today = (int) CampaignTime.Now.ToDays;
            string rumorBlock = RumorStore.GetOrGenerate(settlement, today);

            return new CommonsKnowledge {
               SettlementName = settlement.Name?.ToString(),
               SettlementType = type,
               HolderName = holderName,
               KingdomName = kingdomName,
               Culture = culture,
               ProsperityMood = DescribeProsperity(settlement),
               SecurityNote = DescribeSecurity(settlement),
               ActiveWarsNote = DescribeWars(kingdom),
               LordsPresent = DescribeLords(settlement),
               RumorsBlock = string.IsNullOrWhiteSpace(rumorBlock)
                  ? null
                  : rumorBlock
            };
         }
         catch (Exception)
         {
            // Never crash the main thread over a knowledge snapshot.
            return new CommonsKnowledge {
               SettlementName = settlement.Name?.ToString() ?? "this place",
               SettlementType = "settlement"
            };
         }
      }

      #region private

      private static string DescribeLords(Settlement settlement)
      {
         try
         {
            // Lords present = heroes without a party who are lords visiting this settlement,
            // excluding the settlement's own lord.
            Hero ownerLeader = settlement.OwnerClan?.Leader;
            var names = new List<string>();

            foreach (Hero hero in settlement.HeroesWithoutParty)
            {
               if (!hero.IsLord) continue;
               if (hero == ownerLeader) continue;
               if (hero == Hero.MainHero) continue;
               names.Add(hero.Name?.ToString());

               if (names.Count >= 4) break;
            }

            return names.Count > 0
               ? string.Join(", ", names)
               : null;
         }
         catch { return null; }
      }

      private static string DescribeProsperity(Settlement settlement)
      {
         try
         {
            float p = settlement.IsTown ? settlement.Town?.Prosperity ?? 0f
               : settlement.IsVillage ? settlement.Village?.Hearth ?? 0f
               : 0f;

            if (settlement.IsTown)
            {
               if (p >= 6000f) return "thriving";
               if (p >= 4000f) return "stable";
               if (p >= 2000f) return "struggling";

               return "suffering";
            }

            // Village: hearth is 0–1000+ range
            if (p >= 600f) return "thriving";
            if (p >= 300f) return "stable";
            if (p >= 100f) return "struggling";

            return "suffering";
         }
         catch { return "ordinary"; }
      }

      private static string DescribeSecurity(Settlement settlement)
      {
         try
         {
            if (settlement.IsUnderSiege) return "under siege";

            if (!settlement.IsTown) return null;

            float security = settlement.Town?.Security ?? 100f;

            if (security < 25f) return "wracked by unrest and crime";
            if (security < 50f) return "troubled by banditry on the surrounding roads";

            return null;
         }
         catch { return null; }
      }

      private static string DescribeWars(Kingdom kingdom)
      {
         if (kingdom == null) return null;

         try
         {
            var enemies = new List<string>();
            foreach (Kingdom other in Kingdom.All)
            {
               if (other == kingdom) continue;
               if (FactionManager.IsAtWarAgainstFaction(kingdom, other))
                  enemies.Add(other.Name?.ToString());
            }

            if (enemies.Count == 0) return null;
            if (enemies.Count == 1) return $"the kingdom is at war with {enemies[0]}";

            return $"the kingdom is at war with {string.Join(", ", enemies)}";
         }
         catch { return null; }
      }

      #endregion
   }
}