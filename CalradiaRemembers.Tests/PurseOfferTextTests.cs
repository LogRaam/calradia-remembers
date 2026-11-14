// Code written by Gabriel Mailhot, 14/11/2026.
// What PurseOfferText says: that the purse writes an offer and the coin moves only at the confirmation, and how a player
// tells a payment, a refusal and a bug apart. Aporo (Discord, 14/11/2026) gave 1,000 denars from the bag, read the
// character take them, then declined the [Y/n] that was the payment itself, and reported being asked twice. If this text
// loses its point, players again take the words for the payment and the confirmation for a bug.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PurseOfferTextTests
   {
      // THE REPORT: the purse's window says no coin moves until the player confirms.
      [Test]
      public void GIVEN_the_amount_window_WHEN_its_text_is_read_THEN_it_says_no_coin_moves_before_the_confirmation()
      {
         string text = PurseOfferText.AmountPrompt(2500);

         text.Should().Contain("2,500").And.Contain("no coin moves yet").And.Contain("only then");
      }

      // THE REPORT: the confirmation he declined was the payment, which the Info window says in so many words.
      [Test]
      public void GIVEN_the_info_window_WHEN_read_THEN_it_says_the_confirmation_is_the_payment()
      {
         PurseOfferText.LearnMore().Should().Contain("[Y/n]").And.Contain("Answering yes is the payment");
      }

      // STAKE: a player can tell a refusal from a bug, and knows what to send when it is one.
      [Test]
      public void GIVEN_the_info_window_WHEN_read_THEN_it_tells_a_refusal_from_a_bug_and_names_the_log_lines()
      {
         PurseOfferText.LearnMore().Should().Contain("Refused").And.Contain("[ACTION]").And.Contain("[CONFIRM]").And.Contain("CalradiaRemembers.log");
      }
   }
}
