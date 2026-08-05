// Code written by Gabriel Mailhot, 04/08/2026.
// FollowMeBehavior.Advance calls EscortContinuationPolicy.ShouldEnd every daily tick to decide whether an
// ALREADY-RUNNING escort must stop. These tests pin the STAKE the task itself named: an escort promise must
// end cleanly at its term, and just as cleanly the instant the fiction stops supporting it (the lord dies, is
// captured, or war breaks out) - never silently keep riding along past any of those.

#region

using CalradiaRemembers.Logic.Escort;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Escort
{
    [TestFixture]
    public sealed class EscortContinuationPolicyTests
    {
        // The escort's happy path: days still remain and nothing has broken the fiction, so it must continue.
        [Test]
        public void GIVEN_days_remain_and_nothing_has_broken_the_escort_WHEN_checked_THEN_it_continues()
        {
            EscortContinuationPolicy.ShouldEnd(daysRemaining: 5, isDead: false, isCaptured: false, isAtWar: false)
                .Should().BeFalse();
        }

        // The bounded-term contract itself: a promise the player was told would last a while must actually
        // stop once its days run out, or "for a time" would quietly mean "forever".
        [Test]
        public void GIVEN_the_term_has_run_out_WHEN_checked_THEN_the_escort_ends()
        {
            EscortContinuationPolicy.ShouldEnd(daysRemaining: 0, isDead: false, isCaptured: false, isAtWar: false)
                .Should().BeTrue();
        }

        // Decrementing past zero must never leave the escort stuck open on some future re-read.
        [Test]
        public void GIVEN_days_remaining_went_negative_WHEN_checked_THEN_the_escort_still_ends()
        {
            EscortContinuationPolicy.ShouldEnd(daysRemaining: -1, isDead: false, isCaptured: false, isAtWar: false)
                .Should().BeTrue();
        }

        // A dead escort cannot keep riding alongside anyone - the record must not survive the lord.
        [Test]
        public void GIVEN_the_lord_has_died_WHEN_checked_THEN_the_escort_ends_even_with_days_remaining()
        {
            EscortContinuationPolicy.ShouldEnd(daysRemaining: 10, isDead: true, isCaptured: false, isAtWar: false)
                .Should().BeTrue();
        }

        // A captured lord's party is no longer his to steer - the escort cannot continue while he sits in
        // someone else's dungeon.
        [Test]
        public void GIVEN_the_lord_has_been_captured_WHEN_checked_THEN_the_escort_ends_even_with_days_remaining()
        {
            EscortContinuationPolicy.ShouldEnd(daysRemaining: 10, isDead: false, isCaptured: true, isAtWar: false)
                .Should().BeTrue();
        }

        // War breaking out between the two sides makes the escort a fiction the game world no longer
        // supports - it must end immediately rather than ride out its remaining days as if nothing changed.
        [Test]
        public void GIVEN_war_breaks_out_mid_term_WHEN_checked_THEN_the_escort_ends_even_with_days_remaining()
        {
            EscortContinuationPolicy.ShouldEnd(daysRemaining: 10, isDead: false, isCaptured: false, isAtWar: true)
                .Should().BeTrue();
        }
    }
}
