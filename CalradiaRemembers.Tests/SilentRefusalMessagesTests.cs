// Code written by Gabriel Mailhot, 03/08/2026.
// Player reports, 2026-08-03: a gifted item that never transferred, an NPC's "here's 1000 denars" that
// never arrived, an offered prisoner that stayed in the party. In every case a bridge executor hit a
// refusal branch (no parsable amount, an insolvent giver or player, no matching bargain) and returned a
// bare null instead of an ActionOutcome. A null reaches neither AppendSystemMessage (the player line) nor
// ActionRefusalNote (the model's out-of-character transcript note), so the LLM, having already narrated the
// deed as done, keeps replaying the phantom gift or payment into every later prompt while the player sees
// nothing at all. These strings are what the bridge now hands to ActionOutcome.Refused(...) on those
// branches instead, so both sides of the conversation learn the deed did not happen.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SilentRefusalMessagesTests
   {
      // STAKE: this is the branch a hallucinated or garbled give_gold amount used to hit silently. Without
      // a surfaced message the player never learns why no gold arrived even though the model just said it did.
      [Test]
      public void GIVEN_give_gold_had_no_valid_sum_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.GiveGoldNoValidSum();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: player report 2026-08-03, an NPC's "here's 1000 denars" never arrived. The giver was
      // insolvent, the executor used to just return null, and the model kept treating the promise as kept.
      // The giver's name must be present so the model can attribute the refusal to the right character.
      [Test]
      public void GIVEN_the_giver_has_no_gold_WHEN_building_the_message_THEN_it_names_the_giver_and_denies_the_gift()
      {
         string message = SilentRefusalMessages.GiveGoldGiverInsolvent("Derthert");

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
         message.Should().Contain("Derthert");
      }

      // STAKE: the take_gold twin of GiveGoldNoValidSum; a garbled or missing amount used to vanish
      // silently instead of telling either side that no coin moved.
      [Test]
      public void GIVEN_take_gold_had_no_valid_sum_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.TakeGoldNoValidSum();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: the demand is clamped to the player's real purse (never drives it negative); once that
      // clamp lands at zero the player has nothing to give, and that refusal must reach the model too, or
      // it keeps believing the payment landed.
      [Test]
      public void GIVEN_the_player_has_no_coin_WHEN_building_the_message_THEN_it_is_a_parenthetical_in_character_note()
      {
         string message = SilentRefusalMessages.TakeGoldPlayerInsolvent();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }

      // STAKE: player report 2026-08-03, an offered prisoner stayed in the party. The model emitted
      // give_prisoner with no matching outstanding DeliverPrisoner bargain, the executor used to just
      // return null, and the captive was never actually handed over while the model narrated the handoff.
      [Test]
      public void GIVEN_no_matching_prisoner_bargain_exists_WHEN_building_the_message_THEN_it_denies_the_handoff()
      {
         string message = SilentRefusalMessages.GivePrisonerNoBargain();

         message.Should().NotBeNullOrEmpty();
         message.Should().StartWith("(");
         message.Should().EndWith(")");
      }
   }
}
