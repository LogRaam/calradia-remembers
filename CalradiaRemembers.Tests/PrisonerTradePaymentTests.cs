// Code written by Gabriel Mailhot, 27/09/2026.
// What a captive's buyer may SAY and what is PAID must be the same figure, and a ransom is paid once.
//
// tashmetu's report (Nexus, 27/09/2026, 2.5.25): Domina Ingunde came for a captive and said the ransom was "fifteen
// thousand denars ... I will pay it now". The game paid 5098 (the price is held to a ceiling around the captive's
// ransom value, then to what the buyer can raise), and then tried to pay AGAIN, because the model also emitted a
// give_gold for the words "I will pay it now". Her purse was empty by then, the refusal read "(Domina Ingunde reaches
// for a purse that holds nothing ...)", and the model took that as a fact of the world: next turn she accused the
// player of seizing her treasury, her regard dropped, and she remembers it.
//
// Three rules come out of it, pinned here: the buyer is told before speaking the most they can pay (Gabriel's ruling
// of 09/09/2026: the model must declare already KNOWING the gating, never be refuted after); a figure paid short of
// the one named says why; and a trade that moves coin itself leaves no separate payment standing in the same turn.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PrisonerTradePaymentTests
   {
      // The ceiling bounds a rich buyer: whatever their purse, a captive fetches at most three times its ransom value.
      [Test]
      public void GIVEN_a_rich_buyer_WHEN_asking_the_most_they_can_pay_THEN_it_is_the_captives_ceiling()
      {
         PrisonerTradePolicy.MostABuyerCanPay(ransomAnchor: 1000, buyerGold: 100000).Should().Be(3000);
      }

      // The purse bounds a poor one: Domina Ingunde could not have paid fifteen thousand, and must know it before
      // she names a figure.
      [Test]
      public void GIVEN_a_buyer_short_of_the_ceiling_WHEN_asking_the_most_they_can_pay_THEN_it_is_their_purse()
      {
         PrisonerTradePolicy.MostABuyerCanPay(ransomAnchor: 5000, buyerGold: 5098).Should().Be(5098);
      }

      // An empty purse is nothing, never a negative figure.
      [Test]
      public void GIVEN_an_empty_purse_WHEN_asking_the_most_they_can_pay_THEN_it_is_nothing()
      {
         PrisonerTradePolicy.MostABuyerCanPay(ransomAnchor: 1000, buyerGold: -5).Should().Be(0);
      }

      // The figure paid is the figure named: nothing to explain.
      [Test]
      public void GIVEN_the_named_figure_was_paid_WHEN_noting_the_payment_THEN_there_is_nothing_to_add()
      {
         PrisonerTradePolicy.PriceDifferenceNote(named: 800, paid: 800, limitedByPurse: false, buyerName: "Ingunde").Should().BeEmpty();
      }

      // tashmetu's case: fifteen thousand named, 5098 paid, because that was all she had. The player and the model
      // both read why, so neither invents a reason.
      [Test]
      public void GIVEN_less_was_paid_because_the_purse_ran_out_WHEN_noting_the_payment_THEN_it_says_so()
      {
         PrisonerTradePolicy.PriceDifferenceNote(named: 15000, paid: 5098, limitedByPurse: true, buyerName: "Domina Ingunde")
                            .Should().Be(" (not the 15000 named: that is all Domina Ingunde could raise)");
      }

      [Test]
      public void GIVEN_less_was_paid_because_of_the_ceiling_WHEN_noting_the_payment_THEN_it_says_so()
      {
         PrisonerTradePolicy.PriceDifferenceNote(named: 15000, paid: 3000, limitedByPurse: false, buyerName: "Domina Ingunde")
                            .Should().Be(" (not the 15000 named: that is the most such a captive fetches)");
      }

      // The other way round, when the PLAYER buys: a lord asking less than the floor is held to it, and the player,
      // who pays more than was asked, is told why rather than left to think the lord cheated.
      [Test]
      public void GIVEN_more_was_paid_because_of_the_floor_WHEN_noting_the_payment_THEN_it_says_so()
      {
         PrisonerTradePolicy.PriceDifferenceNote(named: 100, paid: 400, limitedByPurse: false, buyerName: "you")
                            .Should().Be(" (not the 100 named: that is the least such a captive fetches)");
      }

      // No figure named (a bare "price" that did not parse) is no shortfall to explain.
      [Test]
      public void GIVEN_no_figure_was_named_WHEN_noting_the_payment_THEN_there_is_nothing_to_add()
      {
         PrisonerTradePolicy.PriceDifferenceNote(named: 0, paid: 400, limitedByPurse: false, buyerName: "Ingunde").Should().BeEmpty();
      }

      // The double payment itself: a captive bought or sold in this turn carries its own coin, so a give_gold or
      // take_gold beside it is the same payment written twice, and is dropped whichever order they came in.
      [TestCase("give_gold")]
      [TestCase("take_gold")]
      public void GIVEN_a_captive_traded_this_turn_WHEN_a_separate_payment_is_also_emitted_THEN_it_is_the_same_payment(string payment)
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn(payment, new[] {"buy_prisoner", payment}).Should().BeTrue();
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn(payment, new[] {payment, "sell_prisoner"}).Should().BeTrue();
      }

      // The same fault waits beside every verb that moves its own coin, not only a captive's sale.
      [TestCase("exchange_prisoners")]
      [TestCase("pay_for_silence")]
      [TestCase("pay_blackmail")]
      [TestCase("rob_npc")]
      public void GIVEN_another_deal_moving_its_own_coin_WHEN_gold_is_also_emitted_THEN_it_is_the_same_payment(string deal)
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("take_gold", new[] {deal, "take_gold"}).Should().BeTrue();
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("give_gold", new[] {"give_gold", deal}).Should().BeTrue();
      }

      // Without a deal in the turn, gold is gold: a gift, a loan repaid, a bribe.
      [Test]
      public void GIVEN_no_captive_traded_this_turn_WHEN_gold_is_given_THEN_it_stands()
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("give_gold", new[] {"give_gold", "change_relation"}).Should().BeFalse();
      }

      // The trade itself is never mistaken for a separate payment.
      [Test]
      public void GIVEN_a_captive_traded_this_turn_WHEN_reading_the_trade_itself_THEN_it_is_not_a_separate_payment()
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn("buy_prisoner", new[] {"buy_prisoner", "give_gold"}).Should().BeFalse();
      }
   }
}
