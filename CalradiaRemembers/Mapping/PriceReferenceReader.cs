// Code written by Gabriel Mailhot, 01/11/2026.
// Reads the price table from the running game (Gabriel, 01/11/2026: "Il faudrait que le modele connaisse la valeur des choses
// avant de proposer un montant"). Read and not written down, so a modded economy reads true. Once a session: it sits in the
// cached prefix, and a median of the item list is no work to repeat on every turn.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The game's real prices, worded once a session for the prompt.</summary>
   internal static class PriceReferenceReader
   {
      private static bool _read;

      /// <summary>The table for a full prompt, or null when nothing could be read.</summary>
      public static string Full { get { Read(); return _full; } }

      /// <summary>The table in one line, or null when nothing could be read.</summary>
      public static string Compact { get { Read(); return _compact; } }

      private static string _full;
      private static string _compact;

      /// <summary>Forgets the table, so the next campaign reads its own.</summary>
      public static void ResetForSession()
      {
         _read = false;
         _full = null;
         _compact = null;
      }

      private static void Read()
      {
         if (_read) return;
         _read = true;

         try
         {
            var facts = new PriceFacts {
               Grain = DefaultItems.Grain?.Value ?? 0,
               SoldierPay = PayOfTier(3),
               VeteranPay = PayOfTier(5),
               RidingHorse = MedianOf(DefaultItemCategories.Horse),
               WarHorse = MedianOf(DefaultItemCategories.WarHorse),
               GoodSword = MedianOf(DefaultItemCategories.MeleeWeapons3),
               LordRansom = Median(Hero.AllAliveHeroes.Where(h => h.IsLord && h.Clan != null && !h.IsKingdomLeader).Select(CourtActionResolver.RansomAnchor)),
               RulerRansom = Median(Kingdom.All.Where(k => k.Leader != null).Select(k => CourtActionResolver.RansomAnchor(k.Leader)))
            };
            _full = PriceReference.Full(facts);
            _compact = PriceReference.Compact(facts);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PRICES] the price table could not be read: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private static int PayOfTier(int tier)
      {
         CharacterObject troop = CharacterObject.All.FirstOrDefault(c => !c.IsHero && c.Occupation == Occupation.Soldier && c.Tier == tier);

         return troop == null ? 0 : Campaign.Current.Models.PartyWageModel.GetCharacterWage(troop);
      }

      private static int MedianOf(ItemCategory category)
         => category == null ? 0 : Median(TaleWorlds.CampaignSystem.Extensions.Items.All.Where(i => i.ItemCategory == category && !i.NotMerchandise).Select(i => i.Value));

      private static int Median(IEnumerable<int> values)
      {
         List<int> sorted = values.Where(v => v > 0).OrderBy(v => v).ToList();

         return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
      }
   }
}
