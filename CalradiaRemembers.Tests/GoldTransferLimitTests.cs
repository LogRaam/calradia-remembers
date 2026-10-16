// Code written by Gabriel Mailhot, 16/10/2026.
// Whether a gold transfer line says that the ceiling held part of the sum back. tashmetu (Nexus, 16/10/2026): a player
// asked to give 20,000, gave 10,000, and thought the rest had gone missing. Wrong one way, the player believes coin
// vanished; wrong the other way, a line blames the ceiling when it was the purse, or a whole payment, that set the sum.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GoldTransferLimitTests
   {
      // STAKE: the player who asked for more than one transfer may move learns why only part of it went.
      [Test]
      public void GIVEN_a_sum_above_the_ceiling_WHEN_the_ceiling_is_given_THEN_the_line_says_it_is_the_most_at_once()
      {
         GoldTransferLimit.CappedNote(20000, GoldTransferLimit.MaxSingleTransfer).Should().Contain("the most that can change hands at once");
      }

      // STAKE: a whole payment carries no note; nothing was held back.
      [Test]
      public void GIVEN_a_sum_within_the_ceiling_WHEN_it_is_all_given_THEN_the_line_adds_nothing()
      {
         GoldTransferLimit.CappedNote(5000, 5000).Should().BeEmpty();
         GoldTransferLimit.CappedNote(GoldTransferLimit.MaxSingleTransfer, GoldTransferLimit.MaxSingleTransfer).Should().BeEmpty();
      }

      // STAKE: when the purse ran short below the ceiling, blaming the ceiling would be false; the purse figure on the
      // line already tells the player why.
      [Test]
      public void GIVEN_a_sum_above_the_ceiling_and_a_purse_below_it_WHEN_the_purse_is_emptied_THEN_the_line_adds_nothing()
      {
         GoldTransferLimit.CappedNote(20000, 3000).Should().BeEmpty();
      }
   }
}
