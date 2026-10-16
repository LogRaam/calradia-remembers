// Code written by Gabriel Mailhot, 15/10/2026.
// What a hand-over counts for against the war levy, and what the liege is told of the terms in person. Gabriel
// (15/10/2026): a vassal who will not pay the levy in coin "pourrait fournir des troupes, des chevaux, des armes", and
// the liege holds him to account face to face. Wrong one way, a levy can never be met in goods or men, or the liege
// names a way to pay the game will refuse; wrong the other way, a cart of cloth settles a war levy, or paying it
// strips the player's party bare.

#region

using CalradiaRemembers.Logic.WarDuty;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WarLevyPaymentTests
   {
      private static LevyTroopLot Lot(int healthy, int value) => new LevyTroopLot {Healthy = healthy, UnitValue = value};

      // STAKE: Gabriel named horses and arms. Anything else handed to the liege stays a gift, never the levy.
      [Test]
      public void GIVEN_each_kind_of_goods_WHEN_handed_to_the_liege_THEN_only_horses_and_weapons_count_toward_the_levy()
      {
         WarLevyPayment.Counts(LevyGoodsKind.Horse).Should().BeTrue();
         WarLevyPayment.Counts(LevyGoodsKind.Weapon).Should().BeTrue();
         WarLevyPayment.Counts(LevyGoodsKind.Other).Should().BeFalse();
      }

      // STAKE: goods count at their worth, but no more than is owed: a warhorse worth more than the levy settles it,
      // and the outcome says how much was counted.
      [Test]
      public void GIVEN_goods_worth_more_than_is_owed_WHEN_counted_THEN_they_count_for_what_is_owed()
      {
         WarLevyPayment.GoodsCredit(5000, 1200).Should().Be(1200);
         WarLevyPayment.GoodsCredit(800, 1200).Should().Be(800);
      }

      // STAKE: soldiers are taken cheapest first until the levy is met; the vassal keeps his best men.
      [Test]
      public void GIVEN_recruits_and_veterans_WHEN_soldiers_pay_the_levy_THEN_the_cheapest_go_first_until_it_is_met()
      {
         LevyTroopTake take = WarLevyPayment.Troops(new LevyTroopFacts {
            Lots = new[] {Lot(10, 100), Lot(30, 400)}, Outstanding = 1500, Room = 100
         });

         take.Counts.Should().Equal(10, 2);
         take.Men.Should().Be(12);
         take.Value.Should().BeGreaterThanOrEqualTo(1500);
      }

      // STAKE: paying a levy in men must not leave the player's party unable to ride: never more than half of them.
      [Test]
      public void GIVEN_a_levy_worth_the_whole_party_WHEN_soldiers_pay_it_THEN_no_more_than_half_the_men_are_taken()
      {
         LevyTroopTake take = WarLevyPayment.Troops(new LevyTroopFacts {Lots = new[] {Lot(20, 100)}, Outstanding = 100000, Room = 100});

         take.Men.Should().Be(10);
      }

      // STAKE: the men go into the liege's own party; a full party cannot take them.
      [Test]
      public void GIVEN_a_liege_whose_party_is_nearly_full_WHEN_soldiers_pay_THEN_no_more_are_taken_than_it_can_hold()
      {
         WarLevyPayment.Troops(new LevyTroopFacts {Lots = new[] {Lot(20, 100)}, Outstanding = 5000, Room = 3}).Men.Should().Be(3);
         WarLevyPayment.Troops(new LevyTroopFacts {Lots = new[] {Lot(20, 100)}, Outstanding = 5000, Room = 0}).Men.Should().Be(0);
      }

      // STAKE: the summoned vassal stands before a liege who knows what he demands and the one consequence the game
      // will carry out (gating before declaration).
      [Test]
      public void GIVEN_a_levy_partly_paid_WHEN_the_terms_are_worded_THEN_the_liege_knows_what_is_still_owed_and_what_follows()
      {
         string terms = WarDutyWording.Terms(new TermsFacts {LevyAmount = 4000, Outstanding = 2500, DaysLeft = 12, HoldsATakeableFief = true});

         terms.Should().Contain("levy of 4000 denars").And.Contain("2500 are still owed, within 12 days");
         terms.Should().Contain("you will take one of their fiefs");
         terms.Should().Contain("take_gold").And.Contain("give_item");
      }

      // STAKE: soldiers are named as a way to pay only when the game can take them now; otherwise the liege would
      // accept men the game then refuses.
      [Test]
      public void GIVEN_a_liege_who_cannot_take_soldiers_now_WHEN_the_terms_are_worded_THEN_soldiers_are_not_offered()
      {
         WarDutyWording.Terms(new TermsFacts {LevyAmount = 4000, Outstanding = 4000, DaysLeft = 12}).Should().NotContain("give_troops");
         WarDutyWording.Terms(new TermsFacts {LevyAmount = 4000, Outstanding = 4000, DaysLeft = 12, CanTakeTroops = true}).Should().Contain("give_troops");
      }

      // STAKE: a levy fully paid leaves no terms to hold the player to.
      [Test]
      public void GIVEN_nothing_owed_WHEN_the_terms_are_worded_THEN_there_are_none()
      {
         WarDutyWording.Terms(new TermsFacts {LevyAmount = 4000, Outstanding = 0, DaysLeft = 12}).Should().BeNull();
      }
   }
}
