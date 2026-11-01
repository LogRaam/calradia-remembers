// Code written by Gabriel Mailhot, 01/11/2026.
// What this decides: how an exchange is measured and written down, whatever changed hands.
//
// Gabriel (01/11/2026): "Pas seulement l'or.. mais les items aussi. Ces items ont une valeur qui se traduit en or." If this is
// wrong, goods and coin are measured in two units and an exchange cannot be followed or weighed from the log.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ExchangeRecordTests
   {
      // STAKE: Gabriel's unit: goods read in denars, so they can be weighed against coin.
      [Test]
      public void GIVEN_goods_WHEN_they_are_measured_THEN_they_read_in_denars()
      {
         ExchangeRecord.WorthOf(300, 4).Should().Be(1200);
      }

      // STAKE: a stack of costly goods must not turn negative and read as a gift of nothing.
      [Test]
      public void GIVEN_a_huge_stack_WHEN_it_is_measured_THEN_the_sum_does_not_overflow()
      {
         ExchangeRecord.WorthOf(int.MaxValue, 3).Should().Be(int.MaxValue);
      }

      // STAKE: a goods line with no worth to read must not invent one.
      [TestCase(0, 3)]
      [TestCase(300, 0)]
      public void GIVEN_nothing_of_worth_WHEN_it_is_measured_THEN_it_is_nothing(int value, int count)
      {
         ExchangeRecord.WorthOf(value, count).Should().Be(0);
      }

      // STAKE: a report reads the log: who paid whom what, under one tag, whatever road it took.
      [Test]
      public void GIVEN_coin_changed_hands_WHEN_it_is_written_down_THEN_the_log_says_who_paid_whom_what()
      {
         new ExchangeRecord {Kind = ExchangeKind.Gift, From = "Rhagaea", To = "Arwa", Denars = 500}.Line()
            .Should().Be("[EXCHANGE] Gift: Rhagaea -> Arwa: 500 denars");
      }

      // STAKE: goods must read as what they were worth and what they were.
      [Test]
      public void GIVEN_goods_changed_hands_WHEN_they_are_written_down_THEN_the_log_names_them_with_their_worth()
      {
         new ExchangeRecord {Kind = ExchangeKind.ItemGift, From = "Arwa", To = "Rhagaea", Denars = 1200, Goods = "4 Long Swords"}.Line()
            .Should().Contain("1200 denars in 4 Long Swords");
      }
   }
}
