// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08): prisoners become tradeable in conversation, BOTH ways. The NPC may BUY
// a captive the player holds (buy_prisoner: the NPC pays), or SELL one his clan holds to the player
// (sell_prisoner: the player pays). The LLM negotiates the figure in-fiction, but the BRIDGE IS LAW: the price
// it emits is clamped here around the game's own ransom valuation of the captive, so a silver-tongued player
// can haggle within reason yet never talk a lord into absurdity. Opens the bounty-hunting loop (a lord wants a
// prisoner and names a price; the player captures and sells).

namespace CalradiaRemembers.Logic
{
   /// <summary>Pure price discipline for a prisoner sale, either direction. See the file header.</summary>
   public static class PrisonerTradePolicy
   {
      /// <summary>TUNE: no captive ever trades for less than this, whatever the ransom model says.</summary>
      public const int MinPrice = 50;

      /// <summary>TUNE: the floor as a share of the ransom anchor (a hard bargain, but not a gift).</summary>
      public const double FloorFactor = 0.4;

      /// <summary>TUNE: the ceiling as a multiple of the ransom anchor (a premium, but not absurdity).</summary>
      public const double CeilingFactor = 3.0;

      /// <summary>Fallback anchor when the game's ransom valuation cannot be read for this captive.</summary>
      public const int FallbackAnchor = 500;

      /// <summary>
      ///   Clamps the LLM-negotiated price into the honest band around the captive's ransom anchor.
      ///   A non-positive proposed price falls to the floor (the emission promised a sale, so a sale
      ///   at the minimum honest figure beats a broken promise); a non-positive anchor uses the fallback.
      /// </summary>
      public static int ClampPrice(int proposedPrice, int ransomAnchor)
      {
         int anchor = ransomAnchor > 0 ? ransomAnchor : FallbackAnchor;
         var floor = (int) (anchor * FloorFactor);
         if (floor < MinPrice) floor = MinPrice;
         var ceiling = (int) (anchor * CeilingFactor);
         if (ceiling < floor) ceiling = floor;

         if (proposedPrice <= 0) return floor;
         if (proposedPrice < floor) return floor;
         if (proposedPrice > ceiling) return ceiling;

         return proposedPrice;
      }
   }
}
