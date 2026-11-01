// Code written by Gabriel Mailhot, 01/11/2026.
// What this decides: the price table a character reasons from before naming a sum.
//
// Gabriel (01/11/2026, on RaveN017Mod's 20,000 denars): "Il faudrait que le modele connaisse la valeur des choses avant de
// proposer un montant." If this is wrong, a character names sums out of all proportion, or the prompt carries a line that
// says nothing when a figure could not be read from the game.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PriceReferenceTests
   {
      private static PriceFacts Prices() => new() {
         Grain = 12, SoldierPay = 5, VeteranPay = 12, RidingHorse = 760, WarHorse = 3840, GoodSword = 2347, LordRansom = 4912, RulerRansom = 24680
      };

      // STAKE: the report: a character must see what a lord's ransom really is before offering one.
      [Test]
      public void GIVEN_the_prices_of_the_game_WHEN_the_table_is_worded_THEN_the_character_reads_them()
      {
         string table = PriceReference.Full(Prices());

         table.Should().Contain("a lord's ransom: 4,900").And.Contain("a ruler's ransom: 24,000").And.Contain("a soldier's pay: 5 a day");
      }

      // STAKE: a figure the game could not give must not print as a price of nothing, which a model would believe.
      [Test]
      public void GIVEN_a_price_that_could_not_be_read_WHEN_the_table_is_worded_THEN_its_line_is_left_out()
      {
         PriceReference.Full(new PriceFacts {Grain = 12}).Should().NotContain("war horse");
      }

      // STAKE: a table with no figures would be an instruction with nothing to reason from.
      [Test]
      public void GIVEN_nothing_read_WHEN_the_table_is_worded_THEN_there_is_no_table()
      {
         PriceReference.Full(new PriceFacts()).Should().BeNull();
         PriceReference.Compact(new PriceFacts()).Should().BeNull();
      }

      // STAKE: Compact pays for every character; it keeps the scale from a day's pay to a lord, in one line.
      [Test]
      public void GIVEN_a_compact_prompt_WHEN_the_table_is_worded_THEN_it_stays_one_short_line()
      {
         string line = PriceReference.Compact(Prices());

         line.Should().Contain("lord's ransom: 4,900");
         line.Length.Should().BeLessThan(120);
         line.Should().NotContain("\n");
      }

      // STAKE: the table sits in the cached prefix: rounding keeps a small shift in a price from rewriting it.
      [TestCase(2347, 2300)]
      [TestCase(24680, 24000)]
      [TestCase(760, 760)]
      [TestCase(12, 12)]
      public void GIVEN_a_price_WHEN_it_is_said_THEN_it_has_two_significant_figures(int value, int said)
      {
         PriceReference.Rounded(value).Should().Be(said);
      }
   }
}
