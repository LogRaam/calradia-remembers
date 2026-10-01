// Code written by Gabriel Mailhot, 01/10/2026.
// What the towns a character answers for lack, read off their own markets (fkasad, Nexus, 01/10/2026: "a 'bring me'
// iron ore, horses, beer, hardwood, whatever the city needs"). The rule is TownNeedsPolicy's; this reads the game's
// price factor for every valid item category in each town the character answers for (Mapping.HeroSeats, closest
// first) and hands back plain readings. Read live and thrown away: a stored copy would be a second market.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>What the towns a character answers for are short of.</summary>
   public static class TownNeeds
   {
      /// <summary>Every valid item category as this town's market prices it. Empty for anything but a town.</summary>
      public static List<MarketReading> Readings(Settlement place)
      {
         var readings = new List<MarketReading>();
         if (place?.Town?.MarketData == null || !place.IsTown) return readings;

         foreach (ItemCategory c in MBObjectManager.Instance.GetObjectTypeList<ItemCategory>())
         {
            if (c == null || !c.IsValid) continue;

            readings.Add(new MarketReading {
               CategoryId = c.StringId, Name = c.GetName()?.ToString(), IsTradeGood = c.IsTradeGood,
               PriceFactor = place.Town.MarketData.GetPriceFactor(c)
            });
         }

         return readings;
      }

      /// <summary>The good most lacking in the closest town this character answers for, with that town; null when none lacks anything.</summary>
      public static (Settlement Place, MarketReading Good)? MostLackingFor(Hero hero)
      {
         foreach (Settlement place in Towns(hero))
         {
            MarketReading good = TownNeedsPolicy.MostLacking(Readings(place));
            if (good != null) return (place, good);
         }

         return null;
      }

      /// <summary>
      ///   The market key for a good named by <paramref name="word" /> (its name or id) that a town this character
      ///   answers for truly lacks; null otherwise, so a giver never asks for a good his towns are full of.
      /// </summary>
      public static string LackedKey(Hero hero, string word)
      {
         if (string.IsNullOrWhiteSpace(word)) return null;

         string w = word.Trim();
         foreach (Settlement place in Towns(hero))
         {
            MarketReading match = Readings(place).Where(TownNeedsPolicy.Lacks)
                                                 .FirstOrDefault(r => string.Equals(r.Name, w, System.StringComparison.OrdinalIgnoreCase)
                                                                      || string.Equals(r.CategoryId, w, System.StringComparison.OrdinalIgnoreCase));
            if (match != null) return ProvideGoodsPolicy.MarketKey(match.CategoryId);
         }

         return null;
      }

      /// <summary>The market a delivered good goes to: the closest of his towns that lacks it, else the closest of his towns.</summary>
      public static Settlement MarketFor(Hero hero, string categoryId)
      {
         List<Settlement> towns = Towns(hero).ToList();

         return towns.FirstOrDefault(t => Readings(t).Any(r => r.CategoryId == categoryId && TownNeedsPolicy.Lacks(r))) ?? towns.FirstOrDefault();
      }

      private static IEnumerable<Settlement> Towns(Hero hero)
         => HeroSeats.ClosestFirst(hero).Select(seat => seat.Place).Where(p => p != null && p.IsTown);
   }
}
