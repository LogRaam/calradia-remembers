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
      /// <summary>Which way a prisoner trade actually runs, read from WHO HOLDS the named captive.</summary>
      public enum TradeDirection
      {
         /// <summary>The player holds them, so the NPC is buying: buy_prisoner.</summary>
         NpcBuysFromPlayer,

         /// <summary>The NPC's clan holds them, so the NPC is selling: sell_prisoner.</summary>
         NpcSellsToPlayer,

         /// <summary>Nobody named in this conversation holds them; there is no trade to make.</summary>
         NoTrade
      }

      /// <summary>
      ///   The direction the trade MUST take, whatever verb the model reached for. Custody is a fact and the
      ///   label is a word: if the captive stands in the NPC's own cage, the NPC can only be selling.
      ///   <para>
      ///     Lordfadooboo (Nexus, 21/09/2026), trying to buy a Nord lord from a Khuzait one: "twice now i speak
      ///     with him, he states his price and agrees to the transfer, and delivers a text that suggests that the
      ///     transaction is complete, but then i get a CR message that says 'you hold no such captive to sell' but
      ///     im not selling a captive, im buying one. and then the transaction gets blocked even though my money
      ///     is already taken." The model heard the player say "buy" and emitted buy_prisoner, which is the
      ///     mirror verb: buy_prisoner is the NPC buying FROM the player. Both verbs were taught, both specs name
      ///     the other as their anti-pattern, and it picked the wrong one anyway, because "buy" is the word the
      ///     player used. Teaching harder was never going to settle this; custody settles it.
      ///   </para>
      /// </summary>
      public static TradeDirection DirectionFor(bool playerHoldsTarget, bool npcClanHoldsTarget)
         => playerHoldsTarget ? TradeDirection.NpcBuysFromPlayer
          : npcClanHoldsTarget ? TradeDirection.NpcSellsToPlayer
          : TradeDirection.NoTrade;

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

      /// <summary>
      ///   The honest MINIMUM a buyer must actually be able to pay for this captive: the same floor
      ///   <see cref="ClampPrice" /> uses (<see cref="FloorFactor" /> of the anchor, never below
      ///   <see cref="MinPrice" />). A buyer who cannot raise this much is REFUSED rather than handed the
      ///   captive for whatever pittance they hold: GiveGoldAction self-clamps a payment to the payer's purse,
      ///   so without this a broke NPC would "buy" a valuable hostage for its last few denars while the player
      ///   was told the full agreed price. The executor pays the true amount and never below this floor.
      /// </summary>
      public static int MinAcceptablePayment(int ransomAnchor)
      {
         int anchor = ransomAnchor > 0 ? ransomAnchor : FallbackAnchor;
         var floor = (int) (anchor * FloorFactor);

         return floor < MinPrice ? MinPrice : floor;
      }
   }
}
