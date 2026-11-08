// Code written by Gabriel Mailhot, 13/10/2026.
// What these pin: selling an item to a character in chat is one deal, at a price the buyer can pay.
//
// 3chmidt (Nexus, 13/10/2026), selling a crafted sword: "when making a deal, the chat says that I have to pay the price
// that was set, which makes it impossible to sell stuff. Afterwards, even when just paying the money ... the game just
// takes another sword out of my inventory." His screenshot: the buyer says "nine thousand is what it is worth now. The
// coin is yours.", the sword is given, then CR asks "Give Poison Menja 9000 coins? [Y/n]". There was no sale, only a
// gift plus a payment the model had to guess the direction of. Gabriel (13/10/2026): a sell_item deal, confirmed by
// the player. If these fail, the player pays for their own sword, loses a second one, or is promised coin the buyer
// does not have.
//
// 3chmidt again (Nexus, 06/11/2026): the price of an axe came as a give_gold beside the give_item, was judged a gift and
// cut from 9,600 to 100. Gabriel's go (08/11/2026): coin and item crossing in one turn are one sale. If PartsOfASale is
// wrong, a purchase is paid as a gift again, or coin is drawn for an item that never moves.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Items;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Items
{
   [TestFixture]
   public sealed class ItemSaleTests
   {
      // ── The price ──

      // The agreed price is paid when the buyer has it: the sale is exactly the deal struck in the conversation.
      [Test]
      public void GIVEN_a_buyer_who_can_pay_WHEN_the_sale_is_settled_THEN_the_agreed_price_is_paid()
      {
         ItemSaleDecision d = ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 9000, BuyerPurse = 20000, PlayerStillHolds = true});

         d.Refusal.Should().Be(ItemSaleRefusal.None);
         d.Price.Should().Be(9000);
         d.ClampedToPurse.Should().BeFalse();
      }

      // A buyer cannot pay coin they do not have: the price comes down to the purse, and the player is asked at that
      // price, never charged a promise the game cannot keep.
      [Test]
      public void GIVEN_a_buyer_short_of_the_price_WHEN_settled_THEN_the_price_is_what_the_purse_holds_and_it_says_so()
      {
         ItemSaleDecision d = ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 9000, BuyerPurse = 4200, PlayerStillHolds = true});

         d.Price.Should().Be(4200);
         d.ClampedToPurse.Should().BeTrue();
      }

      // A model that writes a wild figure must not move it: the one-transfer ceiling every gold move obeys holds here.
      [Test]
      public void GIVEN_a_wild_price_WHEN_settled_THEN_the_single_transfer_ceiling_holds()
      {
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 5000000, BuyerPurse = 9000000, PlayerStillHolds = true})
                       .Price.Should().Be(Logic.GoldTransferLimit.MaxSingleTransfer);
      }

      // RaveN017Mod, Gabriel (01/11/2026): a sale pays what the thing is worth, so a costly item is not stopped at 10,000.
      [Test]
      public void GIVEN_a_costly_item_WHEN_sold_above_10000_THEN_its_worth_decides_and_not_the_old_ceiling()
      {
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 30000, BuyerPurse = 90000, PlayerStillHolds = true, ItemValue = 12000})
                       .Price.Should().Be(30000);
      }

      // Gabriel (01/11/2026): players must not extract "trop d'or au LLM par rapport a la valeur des choses": a dagger talked
      // up to a fortune sells for what such a thing fetches, and the deal says so.
      [Test]
      public void GIVEN_a_cheap_item_talked_up_WHEN_settled_THEN_the_price_stops_at_its_worth_and_says_so()
      {
         ItemSaleDecision deal = ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 9000, BuyerPurse = 90000, PlayerStillHolds = true, ItemValue = 200});

         deal.Price.Should().Be(200 * ItemSalePolicy.WorthCeilingFactor);
         deal.ClampedToWorth.Should().BeTrue();
         deal.ClampedToPurse.Should().BeFalse();
         ItemSalePolicy.DifferenceNote(deal, 9000).Should().Contain("not the 9000 agreed");
      }

      // No sale without something to sell, someone to pay, or a price at all (a gift is give_item, not a sale at 0).
      [Test]
      public void GIVEN_no_item_no_coin_or_no_price_WHEN_settled_THEN_the_sale_is_refused_with_the_reason()
      {
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 100, BuyerPurse = 500, PlayerStillHolds = false}).Refusal.Should().Be(ItemSaleRefusal.ItemGone);
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 100, BuyerPurse = 0, PlayerStillHolds = true}).Refusal.Should().Be(ItemSaleRefusal.BuyerCannotPay);
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 0, BuyerPurse = 500, PlayerStillHolds = true}).Refusal.Should().Be(ItemSaleRefusal.NoPrice);
      }

      // ── One deal per turn ──

      // THE REPORTED CASE: the interpreter read a sale as a gift plus a payment. Beside a sell_item, the give_item of the
      // same thing is the sale itself read twice ("the game just takes another sword out of my inventory"): it goes.
      [Test]
      public void GIVEN_a_sale_read_with_a_gift_of_the_same_thing_beside_it_WHEN_the_turn_is_settled_THEN_the_gift_goes()
      {
         var batch = new List<TradeBatchAction> {
            new() {Type = "sell_item", Item = "Crafted Two Handed Sword"},
            new() {Type = "give_item", Item = "long two handed sword"},
            new() {Type = "change_relation"}
         };

         TradeBatchPolicy.Superseded(new TradeBatchFacts {Actions = batch}).Should().BeEquivalentTo(new[] {1});
      }

      // With the bag, the gift and the sale are the picked item whatever the model called them: "blade" beside "sword"
      // shares no word, and left standing it would hand the sword over unasked and leave the sale nothing to sell.
      [Test]
      public void GIVEN_a_bag_pick_and_a_gift_named_differently_beside_the_sale_WHEN_settled_THEN_the_gift_still_goes()
      {
         var batch = new List<TradeBatchAction> {new() {Type = "sell_item", Item = "sword"}, new() {Type = "give_item", Item = "blade"}};

         TradeBatchPolicy.Superseded(new TradeBatchFacts {Actions = batch, ItemPickedFromBag = true}).Should().BeEquivalentTo(new[] {1});
      }

      // Without the bag, the roster item each resolves to decides, not the words: two names for one item are one item,
      // two items sharing a word are two.
      [Test]
      public void GIVEN_resolved_items_WHEN_settled_THEN_the_resolved_item_decides_not_the_words()
      {
         var same = new List<TradeBatchAction> {new() {Type = "sell_item", Item = "sword", ResolvedId = "crafted_1"}, new() {Type = "give_item", Item = "blade", ResolvedId = "crafted_1"}};
         var other = new List<TradeBatchAction> {new() {Type = "sell_item", Item = "long sword", ResolvedId = "crafted_1"}, new() {Type = "give_item", Item = "short sword", ResolvedId = "falchion"}};

         TradeBatchPolicy.Superseded(new TradeBatchFacts {Actions = same}).Should().BeEquivalentTo(new[] {1});
         TradeBatchPolicy.Superseded(new TradeBatchFacts {Actions = other}).Should().BeEmpty();
      }

      // ...and the payment the model wrote beside it ("Give Poison Menja 9000 coins?") is the sale's own coin: the
      // existing one-payment-per-deal rule drops it, whichever way it points.
      [Test]
      public void GIVEN_a_sale_and_a_gold_move_in_the_same_turn_WHEN_settled_THEN_the_gold_move_is_the_sales_own_and_goes()
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("take_gold", new[] {"sell_item", "take_gold"}).Should().BeTrue();
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("give_gold", new[] {"give_gold", "sell_item"}).Should().BeTrue();
      }

      // A different gift in the same turn is its own deed, and stays.
      [Test]
      public void GIVEN_a_sale_and_an_unrelated_gift_WHEN_settled_THEN_the_gift_stays()
      {
         var batch = new List<TradeBatchAction> {
            new() {Type = "sell_item", Item = "Crafted Two Handed Sword"},
            new() {Type = "give_item", Item = "Jewelry"}
         };

         TradeBatchPolicy.Superseded(new TradeBatchFacts {Actions = batch}).Should().BeEmpty();
      }

      // No sale in the turn: nothing is touched, exactly as before.
      [Test]
      public void GIVEN_no_sale_WHEN_settled_THEN_nothing_is_dropped()
      {
         TradeBatchPolicy.Superseded(new TradeBatchFacts {Actions = new List<TradeBatchAction> {new() {Type = "give_item", Item = "sword"}}}).Should().BeEmpty();
      }

      // ── Coin and item crossing in one turn ──

      // THE REPORTED CASE (3chmidt, 06/11): Yfinja's coin came as a give_gold and the axe as a give_item. Read as one sale
      // at the coin handed over, it is paid as a sale, not cut to 100 as a gift.
      [Test]
      public void GIVEN_coin_from_the_character_and_an_item_from_the_player_in_one_turn_WHEN_settled_THEN_it_is_one_sale_at_that_coin()
      {
         var batch = new List<TradeBatchAction> {
            new() {Type = "give_gold", Amount = "9600"},
            new() {Type = "change_relation"},
            new() {Type = "give_item", Item = "Ancient Trophy Axe"}
         };

         SaleFromParts sale = TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = batch});

         sale.Should().NotBeNull();
         sale.GoldIndex.Should().Be(0);
         sale.ItemIndex.Should().Be(2);
         sale.Item.Should().Be("Ancient Trophy Axe");
         sale.Price.Should().Be(9600);
      }

      // Coin alone is a gift, and stays one: without an item crossing in the same turn nothing is bought, so the gift
      // ceiling still holds (a sale cannot be claimed to draw coin for nothing).
      [Test]
      public void GIVEN_coin_with_no_item_crossing_WHEN_settled_THEN_it_is_no_sale()
      {
         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "9600"}}})
                         .Should().BeNull();
         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = new List<TradeBatchAction> {new() {Type = "give_item", Item = "axe"}}})
                         .Should().BeNull();
      }

      // A sell_item already in the turn is the sale: its own rules drop the parts, nothing is built from them.
      [Test]
      public void GIVEN_a_sale_already_written_WHEN_settled_THEN_no_second_sale_is_built_from_the_parts()
      {
         var batch = new List<TradeBatchAction> {
            new() {Type = "sell_item", Item = "axe"}, new() {Type = "give_gold", Amount = "9600"}, new() {Type = "give_item", Item = "axe"}
         };

         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = batch}).Should().BeNull();
      }

      // Two purses or two items cannot be paired with certainty: which coin paid for which thing is a guess, so nothing
      // is merged and each runs as written.
      [Test]
      public void GIVEN_two_payments_or_two_items_WHEN_settled_THEN_nothing_is_paired()
      {
         var twoCoins = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "100"}, new() {Type = "give_gold", Amount = "200"}, new() {Type = "give_item", Item = "axe"}};
         var twoItems = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "100"}, new() {Type = "give_item", Item = "axe"}, new() {Type = "give_item", Item = "ring"}};

         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = twoCoins}).Should().BeNull();
         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = twoItems}).Should().BeNull();
      }

      // A sale named in words moves one item: the price written for ten would buy one, so ten in words is left as written.
      // From the bag the amount set there decides, as in any sale.
      [Test]
      public void GIVEN_several_of_an_item_named_in_words_WHEN_settled_THEN_no_sale_is_built_unless_the_bag_set_the_amount()
      {
         var batch = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "900"}, new() {Type = "give_item", Item = "ingot", Count = "10"}};

         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = batch}).Should().BeNull();
         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = batch, ItemPickedFromBag = true}).Should().NotBeNull();
      }

      // At a council table a part carrying another member's name is theirs: pairing it with the speaker's would sell to
      // the wrong buyer.
      [Test]
      public void GIVEN_a_part_belonging_to_another_member_WHEN_settled_THEN_nothing_is_paired()
      {
         var batch = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "900", Actor = "Ira"}, new() {Type = "give_item", Item = "axe"}};

         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = batch}).Should().BeNull();
      }

      // No sum or a sum of nothing is no price.
      [Test]
      public void GIVEN_coin_with_no_clear_sum_WHEN_settled_THEN_it_is_no_sale()
      {
         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "a fair sum"}, new() {Type = "give_item", Item = "axe"}}})
                         .Should().BeNull();
         TradeBatchPolicy.PartsOfASale(new TradeBatchFacts {Actions = new List<TradeBatchAction> {new() {Type = "give_gold", Amount = "0"}, new() {Type = "give_item", Item = "axe"}}})
                         .Should().BeNull();
      }

      // ── What the player is asked ──

      // A sale is always confirmed, and the question names the real item and the real price.
      [Test]
      public void GIVEN_a_sale_WHEN_the_player_is_asked_THEN_the_question_names_the_item_the_buyer_and_the_price()
      {
         ActionConfirmationPolicy.RequiresConfirmation("sell_item").Should().BeTrue();
         ActionConfirmationPolicy.BuildItemQuestion(new ItemConfirmationFacts {
            ActionType = "sell_item", ItemName = "Crafted Two Handed Sword", NpcName = "Poison Menja", Price = 9000
         }).Should().Be("Sell Crafted Two Handed Sword to Poison Menja for 9000 coins?");
      }

      // A price brought down to the purse is said, so the player is not surprised by a smaller sum.
      [Test]
      public void GIVEN_a_price_brought_down_to_the_purse_WHEN_asked_THEN_the_question_says_it_is_all_they_have()
      {
         ActionConfirmationPolicy.BuildItemQuestion(new ItemConfirmationFacts {
            ActionType = "sell_item", ItemName = "Crafted Two Handed Sword", NpcName = "Poison Menja", Price = 4200, ClampedToPurse = true
         }).Should().Be("Sell Crafted Two Handed Sword to Poison Menja for 4200 coins (all they have)?");
      }

      // A gift written in words, not picked from the bag, is confirmed with the item the game would really take: the
      // player sees which sword leaves before it does ("the game just takes another sword out of my inventory").
      [Test]
      public void GIVEN_a_gift_written_in_words_WHEN_asked_THEN_it_is_confirmed_naming_the_item_that_would_leave()
      {
         ActionConfirmationPolicy.GiveItemNeedsConfirmation(pickedFromBag: false).Should().BeTrue();
         ActionConfirmationPolicy.BuildItemQuestion(new ItemConfirmationFacts {
            ActionType = "give_item", ItemName = "Crafted Two Handed Sword", NpcName = "Poison Menja"
         }).Should().Be("Give Crafted Two Handed Sword to Poison Menja?");
      }

      // A gift picked from the bag was chosen by the player item by item: no second question.
      [Test]
      public void GIVEN_a_gift_picked_from_the_bag_WHEN_given_THEN_it_is_not_asked_again()
      {
         ActionConfirmationPolicy.GiveItemNeedsConfirmation(pickedFromBag: true).Should().BeFalse();
      }
   }
}
