// Code written by Gabriel Mailhot, 01/11/2026.
// Gabriel (01/11/2026): "centraliser tout ce qui est transactions dans un service qui s'assure de la qualite de ce processus",
// "Pas seulement l'or.. mais les items aussi. Ces items ont une valeur qui se traduit en or." So an exchange is measured in one
// unit, denars, whatever changed hands, and this is how one is written down.

namespace CalradiaRemembers.Logic
{
   /// <summary>What kind of exchange is being settled.</summary>
   public enum ExchangeKind
   {
      /// <summary>A character gives coin with nothing in return.</summary>
      Gift,

      /// <summary>A captive's ransom or bargain.</summary>
      Ransom,

      /// <summary>A character buys a captive from the player.</summary>
      CaptiveSale,

      /// <summary>The refund of a captive sale that could not complete.</summary>
      CaptiveSaleRefund,

      /// <summary>A character buys an item from the player.</summary>
      ItemSale,

      /// <summary>The player hands an item to a character.</summary>
      ItemGift
   }

   /// <summary>One exchange as the log reads it.</summary>
   public sealed class ExchangeRecord
   {
      /// <summary>What kind of exchange.</summary>
      public ExchangeKind Kind { get; init; }

      /// <summary>Who parted with it.</summary>
      public string From { get; init; }

      /// <summary>Who received it.</summary>
      public string To { get; init; }

      /// <summary>What changed hands, in denars of value, coin or goods alike.</summary>
      public int Denars { get; init; }

      /// <summary>The goods, or null when it was coin.</summary>
      public string Goods { get; init; }

      /// <summary>The line every exchange leaves in the log, so one can be followed whatever road it took.</summary>
      public string Line()
         => $"[EXCHANGE] {Kind}: {From} -> {To}: {Denars} denars{(string.IsNullOrEmpty(Goods) ? "" : $" in {Goods}")}";

      /// <summary>What goods are worth in denars, the one unit of an exchange. Never below nothing, never overflowing.</summary>
      public static int WorthOf(int unitValue, int count)
      {
         if (unitValue <= 0 || count <= 0) return 0;

         long total = unitValue * (long) count;

         return total > int.MaxValue ? int.MaxValue : (int) total;
      }
   }
}
