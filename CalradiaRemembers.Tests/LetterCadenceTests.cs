// Code written by Gabriel Mailhot, 21/06/2026.
// This is the anti-spam half of the letter system, shared by every unprompted letter type (romantic, spouse,
// consort, marriage solicitation, battle/tournament congratulations). LetterTriggerEvaluator's daily tick
// calls straight into these gates to decide whether a letter may queue today. Get it too permissive and the
// player is buried in mail from every correspondent at once (the tester-reported failure GlobalLetterCooldownDays
// exists to stop); get it too restrictive and the world goes mute, breaking the "the world remembers you" feel.
// Two independent layers are pinned here: each NPC's own per-correspondent cooldown, and the cross-NPC global
// throttle that caps the CUMULATIVE flood even when every individual NPC is within their own window.

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
      // Blocks stacking a second letter on top of one still travelling: without this an NPC could queue letter
      // #2 before the player has even received #1, arriving as duplicate or out-of-order mail.
      [Test]
      public void GIVEN_a_letter_still_in_transit_WHEN_checking_THEN_no_new_letter_may_be_sent()
      {
         //Arrange — an undelivered letter due to arrive in the future blocks any second one.
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(98, 105, false));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeFalse();
      }

      // Fails OPEN, the opposite direction from a defensive default: a missing profile reads the same as
      // "never written before", so a not-yet-tracked NPC's very first letter is not silently swallowed by a
      // lookup miss.
      [Test]
      public void GIVEN_a_null_profile_WHEN_checking_THEN_a_letter_is_treated_as_sendable()
      {
         //Act/Assert — degrade to "first letter allowed" rather than throw.
         LetterCadence.CanSendLetter(null, 100, 15).Should().BeTrue();
      }

      // The baseline every correspondence thread starts from: with no prior letters there is no "most recent
      // send" to measure a cooldown against, so the first letter must be allowed outright, not fall through
      // to a crash or a false block.
      [Test]
      public void GIVEN_an_npc_who_has_never_written_WHEN_checking_THEN_the_first_letter_is_allowed()
      {
         //Act/Assert
         LetterCadence.CanSendLetter(Profile(), 100, 15).Should().BeTrue();
      }

      // A stranger with no history must read as "as overdue as possible", not zero: callers gate letters on
      // being apart LONG ENOUGH (the 3/5/7-day checks in LetterTriggerEvaluator), and a bare "0 days" would
      // wrongly exclude an NPC who has simply never been met yet.
      [Test]
      public void GIVEN_no_recorded_events_WHEN_measuring_time_since_last_THEN_the_npc_is_maximally_overdue()
      {
         //Act/Assert — a stranger reads as int.MaxValue, so any "long enough" gate passes.
         LetterCadence.DaysSinceLastEvent(Profile(), 100).Should().Be(int.MaxValue);
      }

      // ── Global throttle (cumulative anti-flood) ────────────────────────────

      // Baseline for the global throttle, mirroring the per-NPC "never written" case: with no global send on
      // record yet, the very first unprompted letter of the playthrough must not be blocked.
      [Test]
      public void GIVEN_no_unprompted_letter_has_ever_been_sent_WHEN_checking_globally_THEN_one_is_allowed()
      {
         LetterCadence.CanSendGlobally(null, 100, 4).Should().BeTrue();
      }

      // The cross-NPC flood guard itself: even when an individual NPC's own cooldown has cleared, the global
      // window keeps the whole world quiet for a few more days, which is what actually stops a player with
      // many correspondents from getting buried (the tester-reported failure GlobalLetterCooldownDays exists
      // to prevent).
      [Test]
      public void GIVEN_one_was_sent_within_the_global_window_WHEN_checking_THEN_the_world_stays_quiet()
      {
         //Arrange — 2 days ago, inside a 4-day global window, even if some NPC's own cooldown is up.
         LetterCadence.CanSendGlobally(98, 100, 4).Should().BeFalse();
      }

      // Must anchor on the MOST RECENT event, not an older one: callers thread this value straight into the
      // generated letter text ("has not seen the player in X days"), so anchoring on a stale event would make
      // the letter lie about how long the NPC has actually been apart from the player.
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

      // Guards against an old letter falsely clearing the cooldown: with several letters on file, only the
      // most recent send may satisfy it, or a correspondent could be made "eligible again" just because their
      // very first letter, from weeks ago, happens to be old enough.
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

      // Confirms the per-NPC cooldown is a window, not a permanent silence: once enough days have passed a
      // correspondent may write again, so one letter early on doesn't mute that NPC for the rest of the
      // playthrough.
      [Test]
      public void GIVEN_the_cooldown_has_elapsed_since_the_last_letter_WHEN_checking_THEN_a_new_one_is_allowed()
      {
         //Arrange — 16 days since the last send, past the 15-day cooldown.
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(84, 86, true));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeTrue();
      }

      // Fixes the boundary as INCLUSIVE (>=, not >): the day the cooldown technically completes must already
      // count as eligible, not require one extra day of margin, so a correspondent isn't kept waiting a day
      // longer than the configured cooldown actually calls for.
      [Test]
      public void GIVEN_the_cooldown_lands_exactly_on_today_WHEN_checking_THEN_it_is_allowed()
      {
         //Arrange — exactly 15 days; the gate is inclusive (today - lastSent >= cooldown).
         NpcProfile p = Profile();
         p.SentLetters.Add(Letter(85, 87, true));

         //Act/Assert
         LetterCadence.CanSendLetter(p, 100, 15).Should().BeTrue();
      }

      // Mirrors the per-NPC expiry case for the GLOBAL throttle: the world-wide quiet period must lift once
      // its window passes, or a single day of heavy correspondence would silence every NPC in the game for
      // good.
      [Test]
      public void GIVEN_the_global_window_has_elapsed_WHEN_checking_THEN_correspondence_may_resume()
      {
         LetterCadence.CanSendGlobally(96, 100, 4).Should().BeTrue();
      }

      // Distinguishes "delivered" from "safe to send again": arrival alone does not clear a correspondent, the
      // cooldown must independently elapse too, so a fast reader can't trigger a second letter early just by
      // opening the first one quickly.
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