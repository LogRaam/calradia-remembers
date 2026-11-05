// Code written by Gabriel Mailhot, 05/11/2026.
// InterpreterTradeBackground decides what the action interpreter reads before it names a captive and a price.
// RaveN017Mod (Nexus, 05/11/2026): sales "still don't work every single time ... especially when I perform several prisoner
// exchanges in a row." The interpreter read one reply alone, so "him" and "the same price" were its guess. If this is wrong,
// the wrong captive is sold or the agreed figure is lost; or, the other way, every turn of every talk pays for lines that
// have nothing to do with captives.

#region

using CalradiaRemembers.Logic.Prisoners;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Prisoners
{
   [TestFixture]
   public sealed class InterpreterTradeBackgroundTests
   {
      // STAKE: RaveN017Mod's second sale: "him" is the captive the player named, and the interpreter now sees that line.
      [Test]
      public void GIVEN_the_player_names_a_captive_WHEN_the_reply_says_him_THEN_the_interpreter_reads_the_captives_and_the_players_line()
      {
         string block = InterpreterTradeBackground.Block(Facts("And Ira? Same terms as Monchug.", "Agreed, I'll take him for the same."));

         block.Should().Contain("Monchug; Ira").And.Contain("PLAYER: And Ira? Same terms as Monchug.");
      }

      // STAKE: "the same price" lives in the reply before; without it the agreed figure is a guess.
      [Test]
      public void GIVEN_a_figure_agreed_in_the_reply_before_WHEN_the_block_is_built_THEN_that_reply_is_quoted()
      {
         string block = InterpreterTradeBackground.Block(new InterpreterTradeFacts {
            HeldCaptives = new[] {"Monchug"}, PlayerLine = "Then it's a deal.", PreviousReply = "Twenty thousand for Monchug, no more.",
            Reply = "Done. My steward will bring the coin."
         });

         block.Should().Contain("YOU (your previous reply): Twenty thousand for Monchug, no more.");
      }

      // STAKE: a talk that is not about captives must read exactly what it read before: no cost, no new way to misread it.
      [Test]
      public void GIVEN_a_turn_about_something_else_WHEN_the_block_is_built_THEN_there_is_none()
      {
         InterpreterTradeBackground.Block(Facts("How fares your wife?", "Well enough, thank you.")).Should().BeNull();
      }

      // STAKE: a player holding nobody has nothing to sell; the block never appears.
      [Test]
      public void GIVEN_no_captive_held_WHEN_the_talk_speaks_of_prisoners_THEN_there_is_none()
      {
         InterpreterTradeBackground.Block(new InterpreterTradeFacts {HeldCaptives = new string[0], PlayerLine = "Buy my prisoner?"}).Should().BeNull();
      }

      // STAKE: "your prisoner" with no name is still a sale under way: the interpreter needs the list to know who.
      [Test]
      public void GIVEN_the_talk_speaks_of_a_prisoner_without_a_name_WHEN_the_block_is_built_THEN_the_captives_are_listed()
      {
         InterpreterTradeBackground.Block(Facts("What would you give for my prisoner?", "Let me think.")).Should().Contain("Monchug; Ira");
      }

      // STAKE: a name inside another word is not that captive ("Ira" in "Iraq-like" talk would add lines to an unrelated turn).
      [Test]
      public void GIVEN_a_captive_name_inside_a_longer_word_WHEN_checked_THEN_the_turn_is_not_about_captives()
      {
         InterpreterTradeBackground.Applies(Facts("Iranel sends her regards.", "How kind.")).Should().BeFalse();
      }

      // STAKE: a long reply must not crowd the interpreter's prompt.
      [Test]
      public void GIVEN_a_very_long_player_line_WHEN_quoted_THEN_it_is_cut()
      {
         string line = "Monchug " + new string('x', 2000);

         InterpreterTradeBackground.Block(Facts(line, "Hm.")).Length.Should().BeLessThan(1600);
      }

      #region private

      private static InterpreterTradeFacts Facts(string playerLine, string reply)
         => new() {HeldCaptives = new[] {"Monchug", "Ira"}, PlayerLine = playerLine, Reply = reply};

      #endregion
   }
}
