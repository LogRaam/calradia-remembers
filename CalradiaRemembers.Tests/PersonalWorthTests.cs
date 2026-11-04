// Code written by Gabriel Mailhot, 03/11/2026.
// What a thing is worth to the one who pays for it. Thoragoros1 (Nexus, 03/11/2026), on 2.8.2's ceilings: "What if a
// character simply values a specific product or type of product above the arbitrary 3x value?" and "There are any number of
// roleplay reasons a captive might be valued above the very low in-game value." Gabriel (03/11/2026) agreed that the ceiling
// should read a reason the GAME holds (the buyer's family, their relation, their gift tastes), never a need told in the chat,
// which the player could tell too (the reason the ceilings exist: 3ae1c82). The prompt's sums and the payment read the same
// rule, so what the character offers is what is paid (RaveN017Mod). If this is wrong, a father pays no more for his son than
// a stranger, or a lord charges the player triple for his own enemy, or a told need empties a purse.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Items;
using CalradiaRemembers.Logic.Prisoners;
using FluentAssertions;
using NpcMemoryService.Core.Parsing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PersonalWorthTests
   {
      // STAKE: Gabriel's "toute la bourse" for a child: a father ransoming his son may offer everything he holds, not three times
      // the game's low valuation.
      [Test]
      public void GIVEN_a_father_buying_his_captive_son_WHEN_the_most_he_pays_is_read_THEN_it_is_his_whole_purse()
      {
         CaptiveBond bond = PrisonerTradePolicy.BondOf(new CaptiveBondFacts {IsChild = true});

         PrisonerTradePolicy.MostABuyerCanPay(1000, 40000, bond).Should().Be(40000);
         PrisonerTradePolicy.ClampPrice(25000, 1000, bond).Should().Be(25000);
      }

      // STAKE: a stranger still meets the old band: a silver tongue cannot talk a lord into paying a fortune for nobody to him.
      [Test]
      public void GIVEN_a_captive_who_is_nothing_to_the_buyer_WHEN_the_price_is_settled_THEN_three_times_the_ransom_still_bounds_it()
      {
         CaptiveBond bond = PrisonerTradePolicy.BondOf(new CaptiveBondFacts {Relation = 5});

         PrisonerTradePolicy.ClampPrice(25000, 1000, bond).Should().Be(3000);
      }

      // STAKE: a brother, a mother, a sworn enemy or a dear friend is worth more than a stranger, but less than one's own child.
      [Test]
      public void GIVEN_kin_or_a_deep_feeling_WHEN_the_price_is_settled_THEN_the_ceiling_is_five_times_the_ransom()
      {
         foreach (CaptiveBondFacts f in new[] {
                     new CaptiveBondFacts {IsParentOrSibling = true}, new CaptiveBondFacts {Relation = -40}, new CaptiveBondFacts {Relation = 30}
                  })
            PrisonerTradePolicy.ClampPrice(25000, 1000, PrisonerTradePolicy.BondOf(f)).Should().Be(5000);
      }

      // STAKE: the bond is the buyer's: when the player buys, a lord must not charge triple-plus for a captive he cares about.
      [Test]
      public void GIVEN_the_player_buying_WHEN_no_bond_is_given_THEN_the_ordinary_band_holds()
      {
         PrisonerTradePolicy.MaxPrice(1000).Should().Be(3000);
      }

      // STAKE: the floor still holds for kin: a broke father is not handed his son for a pittance either.
      [Test]
      public void GIVEN_kin_WHEN_a_low_price_is_named_THEN_the_floor_still_holds()
      {
         PrisonerTradePolicy.ClampPrice(10, 1000, CaptiveBond.SpouseOrChild).Should().Be(PrisonerTradePolicy.MinAcceptablePayment(1000));
      }

      // STAKE: Thoragoros1's case: a lady who loves furs pays more for a fine fur than anyone else would, and no more than that.
      [Test]
      public void GIVEN_a_buyer_who_loves_the_items_kind_WHEN_a_high_price_is_agreed_THEN_up_to_five_times_its_value_is_paid()
      {
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 9000, BuyerPurse = 50000, PlayerStillHolds = true, ItemValue = 1000, BuyerLovesItsKind = true})
            .Price.Should().Be(5000);
         ItemSalePolicy.Settle(new ItemSaleFacts {AgreedPrice = 9000, BuyerPurse = 50000, PlayerStillHolds = true, ItemValue = 1000})
            .Price.Should().Be(3000);
      }

      // STAKE: RaveN017Mod: the sum the character names must be the sum paid. The tokens offered before they speak read the same
      // lifted ceilings as the payment.
      [Test]
      public void GIVEN_a_kin_captive_and_a_loved_item_WHEN_the_tokens_are_set_THEN_their_most_is_what_the_payment_allows()
      {
         IReadOnlyDictionary<string, int> table = MoneyTokenPolicy.Table(new MoneyTokenFacts {
            Purse = 40000, ItemValue = 1000, ItemOfALovedKind = true,
            ToBuy = new List<PricedCaptive> {new() {Name = "Son", RansomAnchor = 1000, BondToBuyer = CaptiveBond.SpouseOrChild}}
         });

         table[MoneyTokens.Key("most", "Son")].Should().Be(40000);
         table[MoneyTokens.Key("most", MoneyTokenPolicy.ItemSubject)].Should().Be(5000);
      }
   }
}
