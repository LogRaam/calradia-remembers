// Code written by Gabriel Mailhot, 13/10/2026.
// Selling an item to a character in chat, as one deal (sell_item).
//
// 3chmidt (Nexus, 13/10/2026): selling a crafted sword, "the chat says that I have to pay the price that was set, which
// makes it impossible to sell stuff. Afterwards ... the game just takes another sword out of my inventory." There was
// no sale verb: the interpreter had to read a sale as give_item plus a gold move, and guessed the gold's direction
// wrong ("The coin is yours." became "Give Poison Menja 9000 coins? [Y/n]"). Gabriel (13/10/2026): a sell_item deal,
// the item and the coin moving together, confirmed by the player at the price the buyer can really pay.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Items
{
   /// <summary>Why a sale does not go through.</summary>
   public enum ItemSaleRefusal
   {
      /// <summary>It goes through.</summary>
      None,

      /// <summary>No price was named: handing it over for nothing is a gift (give_item), not a sale.</summary>
      NoPrice,

      /// <summary>The player no longer holds the item.</summary>
      ItemGone,

      /// <summary>The buyer has no coin to pay with.</summary>
      BuyerCannotPay
   }

   /// <summary>What a sale is settled on.</summary>
   public sealed class ItemSaleFacts
   {
      /// <summary>The price struck in the conversation, in denars.</summary>
      public int AgreedPrice { get; init; }

      /// <summary>What the buyer's purse holds now.</summary>
      public int BuyerPurse { get; init; }

      /// <summary>The player still holds the item being sold.</summary>
      public bool PlayerStillHolds { get; init; }

      /// <summary>The item's market value, which bounds the price (Gabriel, 01/11/2026). Zero when unread: the transfer ceiling then holds.</summary>
      public int ItemValue { get; init; }
   }

   /// <summary>How a sale is settled.</summary>
   public sealed class ItemSaleDecision
   {
      /// <summary>Why it does not go through, or None.</summary>
      public ItemSaleRefusal Refusal { get; init; }

      /// <summary>The price paid: the agreed one, or what the buyer can pay when less.</summary>
      public int Price { get; init; }

      /// <summary>The price was brought down to what the buyer can pay.</summary>
      public bool ClampedToPurse { get; init; }

      /// <summary>The price was brought down to the most such an item fetches.</summary>
      public bool ClampedToWorth { get; init; }
   }

   /// <summary>Settles a sale. Pure.</summary>
   public static class ItemSalePolicy
   {
      /// <summary>TUNE: the most a buyer pays, as a multiple of market value: a premium, not a fortune for a dagger.</summary>
      public const int WorthCeilingFactor = 3;

      /// <summary>The most this item fetches. An unread value falls to the ceiling a figure with no worth behind it gets.</summary>
      public static int MostFor(int itemValue)
         => itemValue > 0 ? Math.Max(itemValue, (int) Math.Min(itemValue * (long) WorthCeilingFactor, int.MaxValue)) : GoldTransferLimit.MaxSingleTransfer;

      /// <summary>
      ///   Gabriel (01/11/2026): a sale is bounded by what the thing is worth, not by a flat 10,000. The agreed price, brought
      ///   down to the buyer's purse and the item's worth; refused when there is no price, no item, or no coin.
      /// </summary>
      public static ItemSaleDecision Settle(ItemSaleFacts f)
      {
         if (f == null || f.AgreedPrice <= 0) return new ItemSaleDecision {Refusal = ItemSaleRefusal.NoPrice};
         if (!f.PlayerStillHolds) return new ItemSaleDecision {Refusal = ItemSaleRefusal.ItemGone};

         int purse = Math.Max(0, f.BuyerPurse);
         if (purse <= 0) return new ItemSaleDecision {Refusal = ItemSaleRefusal.BuyerCannotPay};

         int most = MostFor(f.ItemValue);
         int price = Math.Min(f.AgreedPrice, Math.Min(purse, most));
         bool purseHeld = price < f.AgreedPrice && price == purse;

         return new ItemSaleDecision {Price = price, ClampedToPurse = purseHeld, ClampedToWorth = price < f.AgreedPrice && !purseHeld};
      }

      /// <summary>Added to a sale's notice when less was paid than agreed, so neither the player nor the model is left guessing.</summary>
      public static string DifferenceNote(ItemSaleDecision deal, int agreed)
      {
         if (deal == null || deal.Price >= agreed) return "";

         return deal.ClampedToPurse ? $" (not the {agreed} agreed: that is all they hold)" : $" (not the {agreed} agreed: that is the most such a thing fetches)";
      }
   }
}
