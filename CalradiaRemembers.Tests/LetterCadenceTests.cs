// Code written by Gabriel Mailhot, 21/06/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LetterCadenceTests
   {
      [Test]
      public void GIVEN_a_letter_still_in_transit_WHEN_checking_THEN_no_new_letter_may_be_sent()
      {
         //Arrange — an undelivered letter due to arrive in the future blocks any second one.
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(98, 105, false));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_null_profile_WHEN_checking_THEN_a_letter_is_treated_as_sendable()
      {
         //Act/Assert — degrade to "first letter allowed" rather than throw.
         LetterCadence.CanSendLetter(null, 100, 15).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_npc_who_has_never_written_WHEN_checking_THEN_the_first_letter_is_allowed()
      {
         //Act/Assert
         LetterCadence.CanSendLetter(Profile(), 100, 15).Should().BeTrue();
      }

      [Test]
      public void GIVEN_no_recorded_events_WHEN_measuring_time_since_last_THEN_the_npc_is_maximally_overdue()
      {
         //Act/Assert — a stranger reads as int.MaxValue, so any "long enough" gate passes.
         LetterCadence.DaysSinceLastEvent(Profile(), 100).Should().Be(int.MaxValue);
      }

      // ── Global throttle (cumulative anti-flood) ────────────────────────────

      [Test]
      public void GIVEN_no_unprompted_letter_has_ever_been_sent_WHEN_checking_globally_THEN_one_is_allowed()
      {
         LetterCadence.CanSendGlobally(null, 100, 4).Should().BeTrue();
      }

      [Test]
      public void GIVEN_one_was_sent_within_the_global_window_WHEN_checking_THEN_the_world_stays_quiet()
      {
         //Arrange — 2 days ago, inside a 4-day global window, even if some NPC's own cooldown is up.
         LetterCadence.CanSendGlobally(98, 100, 4).Should().BeFalse();
      }

      [Test]
      public void GIVEN_recorded_events_WHEN_measuring_time_since_last_THEN_it_counts_from_the_most_recent()
      {
         //Arrange
         NpcProfile p = Profile();
         p.Events.Add(new NotableEvent(60, NotableEventType.Other, "old"));
         p.Events.Add(new NotableEvent(93, NotableEventType.Other, "recent"));

         //Act/Assert
         LetterCadence.DaysSinceLastEvent(p, 100).Should().Be(7);
      }

      [Test]
      public void GIVEN_several_past_letters_WHEN_checking_THEN_the_cooldown_runs_from_the_most_recent()
      {
         //Arrange — an old letter would clear the cooldown, but the latest one still gates it.
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(40, 42, true));
         p.SentLetters.Add(Letter(95, 97, true));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_cooldown_has_elapsed_since_the_last_letter_WHEN_checking_THEN_a_new_one_is_allowed()
      {
         //Arrange — 16 days since the last send, past the 15-day cooldown.
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(84, 86, true));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_cooldown_lands_exactly_on_today_WHEN_checking_THEN_it_is_allowed()
      {
         //Arrange — exactly 15 days; the gate is inclusive (today - lastSent >= cooldown).
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(85, 87, true));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_global_window_has_elapsed_WHEN_checking_THEN_correspondence_may_resume()
      {
         LetterCadence.CanSendGlobally(96, 100, 4).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_last_letter_arrived_but_the_cooldown_has_not_elapsed_WHEN_checking_THEN_it_is_blocked()
      {
         //Arrange — delivered, but only 10 days since it was sent against a 15-day cooldown.
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(90, 92, true));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeFalse();
      }

      #region private

      private static PendingLetter Letter(int sentOn, int deliveryDay, bool delivered) => new() {SentOnDay = sentOn, DeliveryDay = deliveryDay, IsDelivered = delivered};

      private static NpcProfile Profile()
         => new() {
            Id = "test_npc",
            Name = "Test Npc",
            Clan = "Test Clan",
            Faction = "Test Faction"
         };

      #endregion
   }
}