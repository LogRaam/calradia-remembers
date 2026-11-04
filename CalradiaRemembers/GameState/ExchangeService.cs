// Code written by Gabriel Mailhot, 01/11/2026.
// Gabriel (01/11/2026): "centraliser tout ce qui est transactions dans un service qui s'assure de la qualite de ce processus",
// "Pas seulement l'or.. mais les items aussi." The one door an exchange with a character goes through: it says what may change
// hands (what the prompt tells the model and the action then pays are one reading), moves it, and writes it down in denars.
// Gold moved in 38 places and each had its own rules, which is how RaveN017Mod met the same bug tashmetu and 3chmidt had.
// Step 1 of Gabriel's plan: the roads already reworked for gold, and give_item. RatchetTests holds the rest from growing.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Commitments;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Items;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>Says what may change hands with a character, moves it, and writes it down.</summary>
   public static class ExchangeService
   {
      /// <summary>TUNE: captives named in the sums taught, as the buy_prisoner teaching lists them: a turn's tail pays for each.</summary>
      private const int TokenCaptives = 3;

      /// <summary>What this character would give the player now with nothing in return: the prompt and give_gold both read this.</summary>
      public static int GiftCeilingFor(Hero giver, NpcProfile profile) => GiftCeilingPolicy.Ceiling(GiftFactsFor(giver, profile));

      /// <summary>What a gift from this character is weighed on.</summary>
      public static GiftFacts GiftFactsFor(Hero giver, NpcProfile profile) => new() {
         Purse = giver?.Gold ?? 0, Regard = profile?.ReputationWithPlayer ?? 0,
         GivenInWindow = GiftCeilingPolicy.GivenStill(profile?.GiftWindowStartDay, profile?.GiftedInWindow ?? 0, (int) CampaignTime.Now.ToDays),
         OwedByOath = CommitmentBehavior.Instance?.OwedBy(giver?.StringId) ?? 0
      };

      /// <summary>Counts what was freely given against the window. What paid a sworn debt was owed, not given.</summary>
      public static void RecordGift(NpcProfile profile, GiftFacts gift, int amount)
      {
         int free = amount - Math.Min(amount, Math.Max(0, gift.OwedByOath));
         if (profile == null || free <= 0) return;

         if (gift.GivenInWindow == 0) profile.GiftWindowStartDay = (int) CampaignTime.Now.ToDays;
         profile.GiftedInWindow = gift.GivenInWindow + free;
      }

      /// <summary>
      ///   The sums this character may name this turn. Reads the same ceiling and the same anchors the actions pay by, so a
      ///   token is a sum the game keeps.
      /// </summary>
      public static IReadOnlyDictionary<string, int> MoneyTokensFor(Hero hero, NpcProfile profile, ItemObject offeredItem, bool offeredItemOfALovedKind)
      {
         // The bond lifts only what this character would pay; when they sell, the player meets the ordinary band.
         IReadOnlyList<PricedCaptive> Priced(IEnumerable<Hero> captives, bool theyBuy) => captives.Take(TokenCaptives)
            .Select(c => new PricedCaptive {
               Name = c.Name?.ToString(), RansomAnchor = CourtActionResolver.RansomAnchor(c),
               BondToBuyer = theyBuy ? BuyPrisonerVerb.BondTo(hero, c) : Logic.Prisoners.CaptiveBond.None
            }).ToList();

         return MoneyTokenPolicy.Table(new MoneyTokenFacts {
            Gift = GiftCeilingFor(hero, profile), Purse = hero.Gold, ItemValue = offeredItem?.Value ?? 0,
            ItemOfALovedKind = offeredItemOfALovedKind,
            ToBuy = Priced(BuyPrisonerVerb.PlayerHeldHeroPrisoners(), true), ToSell = Priced(CourtActionResolver.HeldPrisonersOf(hero), false)
         });
      }

      /// <summary>Moves coin between two people and writes it down. Nothing moves for no sum.</summary>
      public static void Pay(ExchangeKind kind, Hero payer, Hero payee, int amount)
      {
         if (payer == null || payee == null || amount <= 0) return;

         GiveGoldAction.ApplyBetweenCharacters(payer, payee, amount);
         ModLog.Info(new ExchangeRecord {Kind = kind, From = payer.Name?.ToString(), To = payee.Name?.ToString(), Denars = amount}.Line());
      }

      /// <summary>Takes goods out of the player's party for a character, and writes them down at what they are worth.</summary>
      public static void TakeFromPlayer(ExchangeKind kind, EquipmentElement goods, int count, Hero receiver)
      {
         MobileParty.MainParty.ItemRoster.AddToCounts(goods, -count);
         string name = goods.Item?.Name?.ToString();
         ModLog.Info(new ExchangeRecord {
            Kind = kind, From = Hero.MainHero?.Name?.ToString(), To = receiver?.Name?.ToString(),
            Denars = ExchangeRecord.WorthOf(goods.Item?.Value ?? 0, count), Goods = count > 1 ? $"{count} {name}" : name
         }.Line());
      }

      /// <summary>Puts back goods from an exchange that could not complete.</summary>
      public static void ReturnToPlayer(EquipmentElement goods, int count) => MobileParty.MainParty.ItemRoster.AddToCounts(goods, count);
   }
}
