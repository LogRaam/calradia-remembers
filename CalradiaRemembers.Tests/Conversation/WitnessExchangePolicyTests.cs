// Code written by Gabriel Mailhot, 20/10/2026.
// Lordfadooboo (Nexus, 20/10/2026): in a road conversation with a companion invited in, the character asked him a
// question, a bystander spoke, and the character answered the bystander and asked him the same question again, sometimes
// a third time, before he could answer. The policy stops the bystander exchange once the character's line ends on a
// question to the player. If it is wrong, the player is asked the same thing twice or three times, or a bystander who was
// asked by name never gets to answer.

#region

using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class WitnessExchangePolicyTests
   {
      private static readonly string[] Witnesses = {"Ira of Pethros"};

      // STAKE: the reported case: a question to the player is the player's to answer, not something to re-ask after
      // every bystander line.
      [Test]
      public void GIVEN_a_line_ending_on_a_question_to_the_player_WHEN_a_bystander_speaks_THEN_the_exchange_waits_for_the_player()
      {
         WitnessExchangePolicy.WaitsForThePlayer("The roads are hard this season. What brings you this far north?", Witnesses).Should().BeTrue();
      }

      // STAKE: a bystander asked by name is the one who can answer; stopping would leave her question hanging.
      [Test]
      public void GIVEN_a_question_put_to_a_bystander_by_name_WHEN_the_line_is_weighed_THEN_the_exchange_goes_on()
      {
         WitnessExchangePolicy.WaitsForThePlayer("And you, Ira, what do you make of all this?", Witnesses).Should().BeFalse();
      }

      // STAKE: a bystander named earlier in the line is not the one being asked; the closing question is the player's.
      [Test]
      public void GIVEN_a_bystander_named_before_a_closing_question_to_the_player_WHEN_the_line_is_weighed_THEN_the_exchange_waits()
      {
         WitnessExchangePolicy.WaitsForThePlayer("Ira is right about the roads. But what will you do now?", Witnesses).Should().BeTrue();
      }

      // STAKE: a beat very often closes on narration; it must not hide the question that came before it.
      [Test]
      public void GIVEN_a_question_followed_by_a_stage_direction_WHEN_the_line_is_weighed_THEN_the_exchange_waits()
      {
         WitnessExchangePolicy.WaitsForThePlayer("\"Where were you headed?\" *She studies you, waiting.*", Witnesses).Should().BeTrue();
      }

      // STAKE: a statement hands the floor to nobody; the bystander exchange keeps its usual course.
      [Test]
      public void GIVEN_a_line_ending_on_a_statement_WHEN_the_line_is_weighed_THEN_the_exchange_goes_on()
      {
         WitnessExchangePolicy.WaitsForThePlayer("Is it? Then we ride at dawn.", Witnesses).Should().BeFalse();
      }
   }
}
