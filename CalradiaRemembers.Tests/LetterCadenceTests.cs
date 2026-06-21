// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the unprompted-letter cadence: the anti-spam gate every NPC letter passes through. It
// breaks in silence — a regression floods the player with mail, or stacks unread letters on the road,
// or mutes the world entirely.

#region

using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterCadenceTests
    {
        [Test]
        public void GIVEN_an_npc_who_has_never_written_WHEN_checking_THEN_the_first_letter_is_allowed()
        {
            //Act/Assert
            LetterCadence.CanSendLetter(Profile(), today: 100, cooldownDays: 15).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_letter_still_in_transit_WHEN_checking_THEN_no_new_letter_may_be_sent()
        {
            //Arrange — an undelivered letter due to arrive in the future blocks any second one.
            NpcProfile p = Profile();
            p.SentLetters.Add(Letter(sentOn: 98, deliveryDay: 105, delivered: false));

            //Act/Assert
            LetterCadence.CanSendLetter(p, today: 100, cooldownDays: 15).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_last_letter_arrived_but_the_cooldown_has_not_elapsed_WHEN_checking_THEN_it_is_blocked()
        {
            //Arrange — delivered, but only 10 days since it was sent against a 15-day cooldown.
            NpcProfile p = Profile();
            p.SentLetters.Add(Letter(sentOn: 90, deliveryDay: 92, delivered: true));

            //Act/Assert
            LetterCadence.CanSendLetter(p, today: 100, cooldownDays: 15).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_cooldown_has_elapsed_since_the_last_letter_WHEN_checking_THEN_a_new_one_is_allowed()
        {
            //Arrange — 16 days since the last send, past the 15-day cooldown.
            NpcProfile p = Profile();
            p.SentLetters.Add(Letter(sentOn: 84, deliveryDay: 86, delivered: true));

            //Act/Assert
            LetterCadence.CanSendLetter(p, today: 100, cooldownDays: 15).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_cooldown_lands_exactly_on_today_WHEN_checking_THEN_it_is_allowed()
        {
            //Arrange — exactly 15 days; the gate is inclusive (today - lastSent >= cooldown).
            NpcProfile p = Profile();
            p.SentLetters.Add(Letter(sentOn: 85, deliveryDay: 87, delivered: true));

            //Act/Assert
            LetterCadence.CanSendLetter(p, today: 100, cooldownDays: 15).Should().BeTrue();
        }

        [Test]
        public void GIVEN_several_past_letters_WHEN_checking_THEN_the_cooldown_runs_from_the_most_recent()
        {
            //Arrange — an old letter would clear the cooldown, but the latest one still gates it.
            NpcProfile p = Profile();
            p.SentLetters.Add(Letter(sentOn: 40, deliveryDay: 42, delivered: true));
            p.SentLetters.Add(Letter(sentOn: 95, deliveryDay: 97, delivered: true));

            //Act/Assert
            LetterCadence.CanSendLetter(p, today: 100, cooldownDays: 15).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_null_profile_WHEN_checking_THEN_a_letter_is_treated_as_sendable()
        {
            //Act/Assert — degrade to "first letter allowed" rather than throw.
            LetterCadence.CanSendLetter(null, today: 100, cooldownDays: 15).Should().BeTrue();
        }

        [Test]
        public void GIVEN_no_recorded_events_WHEN_measuring_time_since_last_THEN_the_npc_is_maximally_overdue()
        {
            //Act/Assert — a stranger reads as int.MaxValue, so any "long enough" gate passes.
            LetterCadence.DaysSinceLastEvent(Profile(), today: 100).Should().Be(int.MaxValue);
        }

        [Test]
        public void GIVEN_recorded_events_WHEN_measuring_time_since_last_THEN_it_counts_from_the_most_recent()
        {
            //Arrange
            NpcProfile p = Profile();
            p.Events.Add(new NotableEvent(60, NotableEventType.Other, "old"));
            p.Events.Add(new NotableEvent(93, NotableEventType.Other, "recent"));

            //Act/Assert
            LetterCadence.DaysSinceLastEvent(p, today: 100).Should().Be(7);
        }

        // ── Global throttle (cumulative anti-flood) ────────────────────────────

        [Test]
        public void GIVEN_no_unprompted_letter_has_ever_been_sent_WHEN_checking_globally_THEN_one_is_allowed()
        {
            LetterCadence.CanSendGlobally(lastGlobalLetterDay: null, today: 100, cooldownDays: 4).Should().BeTrue();
        }

        [Test]
        public void GIVEN_one_was_sent_within_the_global_window_WHEN_checking_THEN_the_world_stays_quiet()
        {
            //Arrange — 2 days ago, inside a 4-day global window, even if some NPC's own cooldown is up.
            LetterCadence.CanSendGlobally(lastGlobalLetterDay: 98, today: 100, cooldownDays: 4).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_global_window_has_elapsed_WHEN_checking_THEN_correspondence_may_resume()
        {
            LetterCadence.CanSendGlobally(lastGlobalLetterDay: 96, today: 100, cooldownDays: 4).Should().BeTrue();
        }

        #region private

        private static NpcProfile Profile()
            => new NpcProfile
            {
                Id      = "test_npc",
                Name    = "Test Npc",
                Clan    = "Test Clan",
                Faction = "Test Faction"
            };

        private static PendingLetter Letter(int sentOn, int deliveryDay, bool delivered)
            => new PendingLetter { SentOnDay = sentOn, DeliveryDay = deliveryDay, IsDelivered = delivered };

        #endregion
    }
}
