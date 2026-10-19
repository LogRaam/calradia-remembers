// Code written by Gabriel Mailhot, 17/07/2026.
// DIVORCE, announce phase (romance audit C1/C2). This state machine decides, each tick, what to do with a spouse
// demand whose refusal clock has not started because its announcing letter has not (yet) reached the player. It
// exists to kill two real soft-locks: a letter that never arrives (LLM failure or intercepted courier) used to
// freeze the arc forever with no refusal, no escalation, no re-announce; and a stale delivered letter from a
// prior cycle used to falsely start the clock, stamping a refusal for a demand the player never saw. Every step
// below guards one of those failures, so get a boundary wrong and either the arc dead-locks or the player is
// blindsided, both of which this pillar's CHANGELOG line ("you are never blindsided") explicitly forbids.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorceAnnouncePolicyTests
   {
      // The happy path: the CURRENT cycle's letter reached the player, so the clock starts. Delivery must win
      // over every other consideration (window, retry count), because a seen letter is exactly the signal the
      // whole gate waits for.
      [Test]
      public void GIVEN_the_current_letter_is_delivered_WHEN_resolving_THEN_the_clock_starts()
      {
         DivorceAnnouncePolicy.Resolve(new DivorceAnnouncePolicy.AnnounceStateFacts {CurrentLetterDelivered = true, Today = 100, CycleBeginDay = 99, ReannounceCount = 5})
            .Should().Be(DivorceAnnounceStep.StartClock);
      }

      // Within the delivery window and not yet delivered: WAIT. The letter may still be in flight, so acting on
      // its silence now would either blindside the player (force) or waste a re-announce on a letter still coming.
      [Test]
      public void GIVEN_an_undelivered_letter_still_within_its_window_WHEN_resolving_THEN_it_waits()
      {
         DivorceAnnouncePolicy.Resolve(new DivorceAnnouncePolicy.AnnounceStateFacts {CurrentLetterDelivered = false, Today = 105, CycleBeginDay = 100, ReannounceCount = 0})
            .Should().Be(DivorceAnnounceStep.Wait);
      }

      // The C1 core: the window lapsed with no delivery (LLM failed or the courier was intercepted). Rather than
      // freeze forever at "clock not started", re-announce, giving the arc a fresh chance instead of a soft-lock.
      [Test]
      public void GIVEN_the_window_lapsed_undelivered_and_retries_remain_WHEN_resolving_THEN_it_reannounces()
      {
         DivorceAnnouncePolicy.Resolve(new DivorceAnnouncePolicy.AnnounceStateFacts {CurrentLetterDelivered = false, Today = 108, CycleBeginDay = 100, ReannounceCount = 0})
            .Should().Be(DivorceAnnounceStep.Reannounce);
      }

      // The safety valve: after MaxReannounces lapsed windows, stop trusting the letter path entirely and force a
      // guaranteed in-person notice. Without this a persistently-failing LLM or courier would re-announce forever,
      // and the demand would still never resolve, so the arc must escape to a path that cannot fail.
      [Test]
      public void GIVEN_the_retry_budget_is_exhausted_WHEN_resolving_THEN_it_forces_a_notice()
      {
         DivorceAnnouncePolicy.Resolve(new DivorceAnnouncePolicy.AnnounceStateFacts {CurrentLetterDelivered = false, Today = 130, CycleBeginDay = 120, ReannounceCount = DivorceAnnouncePolicy.MaxReannounces})
            .Should().Be(DivorceAnnounceStep.ForceNotice);
      }

      // The window boundary is inclusive of the lapse: exactly AnnounceWindowDays after the cycle began, the host
      // acts (here re-announce), so a letter given its full window but no more does not linger a day longer.
      [Test]
      public void GIVEN_exactly_the_window_has_elapsed_WHEN_resolving_THEN_it_acts_rather_than_waiting()
      {
         DivorceAnnouncePolicy.Resolve(new DivorceAnnouncePolicy.AnnounceStateFacts {CurrentLetterDelivered = false, Today = 100 + DivorceAnnouncePolicy.AnnounceWindowDays, CycleBeginDay = 100, ReannounceCount = 0})
            .Should().Be(DivorceAnnounceStep.Reannounce);
      }
   }
}
