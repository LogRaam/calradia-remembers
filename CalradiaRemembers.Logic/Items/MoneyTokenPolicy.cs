// Code written by Gabriel Mailhot, 01/11/2026.
// Gabriel (01/11/2026, on RaveN017Mod's 20,000 denars): "le LLM offre {gold_amount} sans que le montant soit specifie. Lorsque
// nous recevons ce tag, on utilise un calculateur qui remplace le tag par une somme que nous controlons." This is that
// calculator: each sum a character may name this turn, already within what it can pay and what the thing is worth, so a token
// is a promise the game keeps. Low, fair and most leave the character a stance in a negotiation without a figure to invent.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic.Prisoners;
using NpcMemoryService.Core.Parsing;

#endregion

namespace CalradiaRemembers.Logic.Items
{
   /// <summary>A captive up for trade, by the name the prompt lists them under.</summary>
   public sealed class PricedCaptive
   {
      /// <summary>The name, as the prompt teaches it.</summary>
      public string Name { get; init; }

      /// <summary>What the game values them at.</summary>
      public int RansomAnchor { get; init; }
   }

   /// <summary>What this turn's sums are computed from.</summary>
   public sealed class MoneyTokenFacts
   {
      /// <summary>What the character would give with nothing in return (GiftCeilingPolicy).</summary>
      public int Gift { get; init; }

      /// <summary>What the character's purse holds.</summary>
      public int Purse { get; init; }

      /// <summary>Captives the player holds, which this character could buy with their own purse.</summary>
      public IReadOnlyList<PricedCaptive> ToBuy { get; init; }

      /// <summary>Captives this character holds, which the player could buy: the player's purse is the player's to answer for.</summary>
      public IReadOnlyList<PricedCaptive> ToSell { get; init; }

      /// <summary>The market value of the item held out this turn, or 0 when none is.</summary>
      public int ItemValue { get; init; }
   }

   /// <summary>The sums a character may name this turn. Pure.</summary>
   public static class MoneyTokenPolicy
   {
      /// <summary>The subject of the tokens for the item held out this turn: "{fair:item}".</summary>
      public const string ItemSubject = "item";

      /// <summary>TUNE: the opening of a hard bargain for an item, as a share of its market value.</summary>
      public const double ItemLowFactor = 0.5;

      /// <summary>The table, keyed as the tokens are written, in the order they are taught. A sum nobody could pay is left out.</summary>
      public static IReadOnlyDictionary<string, int> Table(MoneyTokenFacts f)
      {
         var table = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
         if (f == null) return table;

         if (f.Gift > 0) table[MoneyTokens.Gift] = f.Gift;

         foreach (PricedCaptive captive in f.ToBuy ?? new List<PricedCaptive>())
         {
            int low = PrisonerTradePolicy.MinAcceptablePayment(captive.RansomAnchor);
            int most = PrisonerTradePolicy.MostABuyerCanPay(captive.RansomAnchor, f.Purse);
            if (most >= low) Add(table, captive.Name, low, Math.Min(captive.RansomAnchor, most), most);
         }

         foreach (PricedCaptive captive in f.ToSell ?? new List<PricedCaptive>())
            Add(table, captive.Name, PrisonerTradePolicy.MinAcceptablePayment(captive.RansomAnchor), captive.RansomAnchor, PrisonerTradePolicy.MaxPrice(captive.RansomAnchor));

         if (f.ItemValue > 0 && f.Purse > 0)
         {
            int most = Math.Min(ItemSalePolicy.MostFor(f.ItemValue), f.Purse);
            Add(table, ItemSubject, Math.Min((int) (f.ItemValue * ItemLowFactor), most), Math.Min(f.ItemValue, most), most);
         }

         return table;
      }

      private static void Add(Dictionary<string, int> table, string subject, int low, int fair, int most)
      {
         if (string.IsNullOrWhiteSpace(subject)) return;

         table[MoneyTokens.Key("low", subject)] = low;
         table[MoneyTokens.Key("fair", subject)] = fair;
         table[MoneyTokens.Key("most", subject)] = most;
      }
   }
}
