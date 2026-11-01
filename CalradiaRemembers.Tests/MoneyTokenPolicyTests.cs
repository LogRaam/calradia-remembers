// Code written by Gabriel Mailhot, 01/11/2026.
// What this decides: which sums a character may name this turn, the figures a money token fills in.
//
// Gabriel (01/11/2026, on RaveN017Mod's 20,000 denars): the LLM proposes with a placeholder and "on utilise un calculateur qui
// remplace le tag par une somme que nous controlons". If this is wrong, a token promises what the game will not pay, and what
// the character says and what the player receives part ways again.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Items;
using CalradiaRemembers.Logic.Prisoners;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MoneyTokenPolicyTests
   {
      private static PricedCaptive Monchug(int anchor = 5000) => new() {Name = "Monchug", RansomAnchor = anchor};

      // STAKE: the token must pay exactly what give_gold will: the ceiling the action reads.
      [Test]
      public void GIVEN_a_gift_ceiling_WHEN_the_table_is_built_THEN_the_gift_token_is_that_ceiling()
      {
         MoneyTokenPolicy.Table(new MoneyTokenFacts {Gift = 500})["gift"].Should().Be(500);
      }

      // STAKE: a character who would give nothing must have no gift token to write.
      [Test]
      public void GIVEN_nothing_to_give_WHEN_the_table_is_built_THEN_there_is_no_gift_token()
      {
         MoneyTokenPolicy.Table(new MoneyTokenFacts {Gift = 0}).Should().NotContainKey("gift");
      }

      // STAKE: the band buy_prisoner already enforces: the three stances must match what its executor will pay.
      [Test]
      public void GIVEN_a_captive_to_buy_and_a_full_purse_WHEN_the_table_is_built_THEN_the_sums_are_the_verbs_own_band()
      {
         IReadOnlyDictionary<string, int> t = MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 100000, ToBuy = new[] {Monchug()}});

         t["low:Monchug"].Should().Be(PrisonerTradePolicy.MinAcceptablePayment(5000));
         t["fair:Monchug"].Should().Be(5000);
         t["most:Monchug"].Should().Be(PrisonerTradePolicy.MaxPrice(5000));
      }

      // STAKE: the report: a figure beyond the purse. Rhagaea cannot be offered a token she could not pay.
      [Test]
      public void GIVEN_a_purse_below_the_fair_price_WHEN_the_table_is_built_THEN_no_token_exceeds_it()
      {
         IReadOnlyDictionary<string, int> t = MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 3000, ToBuy = new[] {Monchug()}});

         t["fair:Monchug"].Should().Be(3000);
         t["most:Monchug"].Should().Be(3000);
      }

      // STAKE: below the floor no sale goes through, so a token would be a promise broken on the spot.
      [Test]
      public void GIVEN_a_purse_below_the_least_price_WHEN_the_table_is_built_THEN_the_captive_has_no_tokens()
      {
         MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 100, ToBuy = new[] {Monchug()}}).Should().BeEmpty();
      }

      // STAKE: selling, the player pays, so the character's own purse does not bound the price.
      [Test]
      public void GIVEN_a_captive_to_sell_WHEN_the_table_is_built_THEN_the_sellers_purse_does_not_lower_the_price()
      {
         MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 10, ToSell = new[] {Monchug()}})["fair:Monchug"].Should().Be(5000);
      }

      // STAKE: 3chmidt's sale: the most a buyer can name is what sell_item will pay.
      [Test]
      public void GIVEN_an_item_held_out_WHEN_the_table_is_built_THEN_its_sums_stop_at_its_worth_and_the_purse()
      {
         IReadOnlyDictionary<string, int> t = MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 100000, ItemValue = 200});

         t["low:item"].Should().Be(100);
         t["fair:item"].Should().Be(200);
         t["most:item"].Should().Be(ItemSalePolicy.MostFor(200));
      }

      // STAKE: a buyer with an empty purse gets no token to promise with.
      [Test]
      public void GIVEN_an_empty_purse_WHEN_an_item_is_held_out_THEN_it_has_no_tokens()
      {
         MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 0, ItemValue = 200}).Should().BeEmpty();
      }

      // STAKE: a model writes the name its own way; the fill must not depend on the case.
      [Test]
      public void GIVEN_the_table_WHEN_a_key_is_read_in_another_case_THEN_the_sum_is_found()
      {
         MoneyTokenPolicy.Table(new MoneyTokenFacts {Purse = 100000, ToBuy = new[] {Monchug()}}).Should().ContainKey("FAIR:monchug");
      }
   }
}
