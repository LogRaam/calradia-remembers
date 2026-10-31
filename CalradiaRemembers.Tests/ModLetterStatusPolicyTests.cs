// Code written by Gabriel Mailhot, 20/10/2026.
// What another mod is told of a letter it delivered through CR. tashmetu (20/10/2026): R&S found its tournament invitation
// on the sender's profile by sender, day and text, read the reply by reflection every hour, and could not tell "read and
// set aside" from "not read yet". By its id a letter can be asked about: "delivered yet, lost on the road, answered". If
// this is wrong, R&S seats a player at a tournament he declined, counts as unanswered a letter he answered, or as refused
// one that never reached him.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ModLetterStatusPolicyTests
   {
      // STAKE: a letter still with its courier has not been refused; R&S must not read silence on the road as a decline.
      [Test]
      public void GIVEN_a_letter_its_courier_still_carries_WHEN_asked_THEN_it_is_on_the_road()
      {
         ModLetterStatusPolicy.Of(new ModLetterFacts()).Should().Be(ModLetterStatusPolicy.OnTheRoad);
      }

      // STAKE: "lost on the road", in tashmetu's words: a letter that never arrived was never declined either.
      [Test]
      public void GIVEN_a_letter_whose_courier_was_lost_WHEN_asked_THEN_it_is_lost()
      {
         ModLetterStatusPolicy.Of(new ModLetterFacts {IsLost = true}).Should().Be(ModLetterStatusPolicy.Lost);
      }

      // STAKE: a lost letter CR sent again and that finally arrived is in the player's hands; the old loss no longer counts.
      [Test]
      public void GIVEN_a_lost_letter_that_arrived_after_all_WHEN_asked_THEN_it_is_delivered()
      {
         ModLetterStatusPolicy.Of(new ModLetterFacts {IsLost = true, IsDelivered = true}).Should().Be(ModLetterStatusPolicy.Delivered);
      }

      // STAKE: "read and set aside" versus "not read yet", the distinction tashmetu could not make.
      [Test]
      public void GIVEN_a_letter_the_player_read_and_set_aside_WHEN_asked_THEN_it_is_set_aside()
      {
         ModLetterStatusPolicy.Of(new ModLetterFacts {IsDelivered = true, SetAside = true}).Should().Be(ModLetterStatusPolicy.SetAsideStatus);
      }

      // STAKE: a reply is the player's word on the letter; once sealed it outranks every earlier state.
      [Test]
      public void GIVEN_a_letter_the_player_answered_WHEN_asked_THEN_it_is_answered()
      {
         ModLetterStatusPolicy.Of(new ModLetterFacts {IsDelivered = true, SetAside = true, HasReply = true}).Should().Be(ModLetterStatusPolicy.Answered);
      }

      // STAKE: an id CR does not know names no letter; the mod must hear that, not a status that sounds like news.
      [Test]
      public void GIVEN_no_letter_WHEN_asked_THEN_it_is_unknown()
      {
         ModLetterStatusPolicy.Of(null).Should().Be(ModLetterStatusPolicy.Unknown);
      }

      // STAKE: tashmetu asked to tell "read" from "not read yet" (20/10/2026); since the letter box (2.8.0), a letter that
      // reached the player but waits unopened says so, rather than "delivered" as though it had been read.
      [Test]
      public void GIVEN_a_letter_waiting_unopened_in_the_letter_box_WHEN_asked_THEN_it_is_in_the_box()
      {
         ModLetterStatusPolicy.Of(new ModLetterFacts {IsDelivered = true, InBox = true}).Should().Be(ModLetterStatusPolicy.InTheBox);
      }
}
}
