// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08): prisoners become tradeable in conversation, BOTH ways. The NPC may BUY
// a captive the player holds (buy_prisoner: the NPC pays), or SELL one his clan holds to the player
// (sell_prisoner: the player pays). The LLM negotiates the figure in-fiction, but the BRIDGE IS LAW: the price
// it emits is clamped here around the game's own ransom valuation of the captive, so a silver-tongued player
// can haggle within reason yet never talk a lord into absurdity. Opens the bounty-hunting loop (a lord wants a
// prisoner and names a price; the player captures and sells).
// Thoragoros1 (Nexus, 03/11/2026): "There are any number of roleplay reasons a captive might be valued above the very low
// in-game value." Gabriel (03/11/2026) agreed the ceiling should read a reason the GAME holds, never one claimed in the chat:
// a buyer pays more for their own kin, and for someone they deeply love or hate, by the engine's family and relation.

#region

using System;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic.Prisoners
{
   /// <summary>What a captive is to the character buying them, as the game knows it. Only a buyer paying reads it.</summary>
   public enum CaptiveBond
   {
      /// <summary>Nothing the game knows of: the ordinary band.</summary>
      None,

      /// <summary>Their parent, brother or sister.</summary>
      Kin,

      /// <summary>Their spouse or their own child: they would give everything they hold.</summary>
      SpouseOrChild,

      /// <summary>Someone they deeply love or bitterly hate, by the game's relation.</summary>
      StrongFeeling
   }

   /// <summary>What decides a captive's bond to their buyer. Read from the engine's family and relation, never from words.</summary>
   public sealed class CaptiveBondFacts
   {
      /// <summary>The captive is the buyer's spouse.</summary>
      public bool IsSpouse { get; init; }

      /// <summary>The captive is the buyer's child.</summary>
      public bool IsChild { get; init; }

      /// <summary>The captive is the buyer's father, mother, brother or sister.</summary>
      public bool IsParentOrSibling { get; init; }

      /// <summary>The game's relation between buyer and captive.</summary>
      public int Relation { get; init; }
   }

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

      /// <summary>
      ///   TUNE: the captives a buyer is taught a price for and offered money tokens for. Was 3 (RaveN017Mod, Nexus, 05/11/2026:
      ///   from the fourth captive on the character had no figure and improvised); one short line each.
      /// </summary>
      public const int CaptivesPriced = 8;

      /// <summary>TUNE: no captive ever trades for less than this, whatever the ransom model says.</summary>
      public const int MinPrice = 50;

      /// <summary>TUNE: the floor as a share of the ransom anchor (a hard bargain, but not a gift).</summary>
      public const double FloorFactor = 0.4;

      /// <summary>TUNE: the ceiling as a multiple of the ransom anchor (a premium, but not absurdity).</summary>
      public const double CeilingFactor = 3.0;

      /// <summary>TUNE: the ceiling, in ransom anchors, for a buyer's parent, brother or sister, or someone they love or hate.</summary>
      public const double PersonalCeilingFactor = 5.0;

      /// <summary>What the captive is to the buyer. A spouse or child first, then kin, then a strong feeling.</summary>
      public static CaptiveBond BondOf(CaptiveBondFacts f)
      {
         if (f == null) return CaptiveBond.None;
         if (f.IsSpouse || f.IsChild) return CaptiveBond.SpouseOrChild;
         if (f.IsParentOrSibling) return CaptiveBond.Kin;

         // The game's close friend and bitter enemy rungs, either way a captive who matters to the buyer.
         return f.Relation >= VanillaRelation.CloseTier || f.Relation <= VanillaRelation.BitterTier ? CaptiveBond.StrongFeeling : CaptiveBond.None;
      }

      /// <summary>The ceiling in ransom anchors a buyer with this bond pays up to. A spouse or child has none: the purse bounds it.</summary>
      public static double CeilingFactorFor(CaptiveBond bond)
      {
         switch (bond)
         {
            case CaptiveBond.SpouseOrChild: return double.PositiveInfinity;
            case CaptiveBond.Kin:
            case CaptiveBond.StrongFeeling: return PersonalCeilingFactor;
            default: return CeilingFactor;
         }
      }

      /// <summary>Fallback anchor when the game's ransom valuation cannot be read for this captive.</summary>
      public const int FallbackAnchor = 500;

      /// <summary>
      ///   Clamps the LLM-negotiated price into the honest band around the captive's ransom anchor.
      ///   A non-positive proposed price falls to the floor (the emission promised a sale, so a sale
      ///   at the minimum honest figure beats a broken promise); a non-positive anchor uses the fallback.
      /// </summary>
      /// <remarks>
      ///   <paramref name="bond" /> is what the captive is to the BUYER, and is given only when the character pays (buy_prisoner):
      ///   when the player buys, the ordinary band holds, so a lord never charges the player more for his own enemy.
      /// </remarks>
      public static int ClampPrice(int proposedPrice, int ransomAnchor, CaptiveBond bond = CaptiveBond.None)
      {
         int anchor = ransomAnchor > 0 ? ransomAnchor : FallbackAnchor;
         var floor = (int) (anchor * FloorFactor);
         if (floor < MinPrice) floor = MinPrice;
         var ceiling = (int) Math.Min(int.MaxValue, anchor * CeilingFactorFor(bond));
         if (ceiling < floor) ceiling = floor;

         if (proposedPrice <= 0) return floor;
         if (proposedPrice < floor) return floor;
         if (proposedPrice > ceiling) return ceiling;

         return proposedPrice;
      }

      /// <summary>
      ///   The most a buyer can actually pay for this captive: the price ceiling, or their purse when it holds less.
      ///   Taught BEFORE the buyer names a figure (tashmetu, 27/09/2026: a lord offered fifteen thousand denars she
      ///   did not have, and the game paid 5098), per Gabriel's ruling that the model declares already knowing the
      ///   gating rather than being refuted after.
      /// </summary>
      /// <summary>The most any buyer pays for this captive (the ceiling of <see cref="ClampPrice" />).</summary>
      public static int MaxPrice(int ransomAnchor, CaptiveBond bond = CaptiveBond.None) => ClampPrice(int.MaxValue, ransomAnchor, bond);

      public static int MostABuyerCanPay(int ransomAnchor, int buyerGold, CaptiveBond bond = CaptiveBond.None)
      {
         return Math.Max(0, Math.Min(MaxPrice(ransomAnchor, bond), buyerGold));
      }

      /// <summary>
      ///   What to add to a payment's notice when the figure paid differs from the one named, and why: all the buyer
      ///   could raise, the most such a captive fetches, or the least. Empty when the figure named was paid, or none was
      ///   named. Read by the player and by the model, so neither invents a reason for the difference.
      /// </summary>
      /// <summary>
      ///   The refusal when the lord who agreed to sell a captive does not hold them. Lordfadooboo (Nexus, 04/10/2026):
      ///   "You hold no such captive to sell" answered a purchase, and read as though the player were the seller. Says
      ///   who does hold the captive, so the player knows whom to treat with, or why the captive is not for sale.
      /// </summary>
      public static string NotHisToSellNote(NotHisToSellFacts f)
      {
         if (f == null) return "(There is no such captive to trade.)";
         if (f.IsPlayersOwn) return $"({f.CaptiveName} is one of your own, and not for sale. Ask for their release, or offer an exchange.)";
         // froggyluv (Nexus, 04/11/2026): a lady BUYING his captive was told she "holds no captive called Elthild", which read
         // as the sale reversed. When the player was the seller, the refusal speaks of the player's keeping.
         if (f.PlayerWasSelling)
         {
            if (string.IsNullOrWhiteSpace(f.HolderName)) return $"(You hold no captive called {f.CaptiveName} for {f.SellerName} to buy. Nothing is paid.)";

            string held = string.IsNullOrWhiteSpace(f.PlaceName) ? "" : $", at {f.PlaceName}";

            return $"({f.CaptiveName} is not in your keeping: {f.HolderName} holds them{held}. Nothing is paid.)";
         }
         if (string.IsNullOrWhiteSpace(f.HolderName)) return $"({f.SellerName} holds no captive called {f.CaptiveName}. Nothing is paid.)";

         string where = string.IsNullOrWhiteSpace(f.PlaceName) ? "" : $", at {f.PlaceName}";

         return $"({f.SellerName} does not hold {f.CaptiveName}: {f.HolderName} does{where}. Nothing is paid; treat with whoever holds them.)";
      }

      public static string PriceDifferenceNote(int named, int paid, bool limitedByPurse, string buyerName)
      {
         if (named <= 0 || paid == named) return "";
         if (paid > named) return $" (not the {named} named: that is the least such a captive fetches)";

         return limitedByPurse
            ? $" (not the {named} named: that is all {buyerName} could raise)"
            : $" (not the {named} named: that is the most such a captive fetches)";
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

   /// <summary>What the refusal of a sale by a lord who does not hold the captive is told from.</summary>
   public sealed class NotHisToSellFacts
   {
      /// <summary>The lord who agreed to sell.</summary>
      public string SellerName { get; init; }

      public string CaptiveName { get; init; }

      /// <summary>Who really holds the captive (a lord, or a house), or empty when no captive by that name is held anywhere.</summary>
      public string HolderName { get; init; }

      /// <summary>Where the captive is held (a town, a castle, a party), when known.</summary>
      public string PlaceName { get; init; }

      /// <summary>The captive is of the player's own house: never merchandise.</summary>
      public bool IsPlayersOwn { get; init; }

      /// <summary>The player was the seller (buy_prisoner): the refusal speaks of the player's keeping, not the buyer's.</summary>
      public bool PlayerWasSelling { get; init; }
   }
}
