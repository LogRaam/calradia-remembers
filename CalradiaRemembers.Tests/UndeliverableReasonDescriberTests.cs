// Code written by Gabriel Mailhot, 20/07/2026.
// Documents the failure-report wording: which cause an undeliverable letter resolves to, and the exact line the
// player reads. Before this batch every failure said "could not be reached", so the player never learned that
// the recipient DIED or was CAPTURED mid-flight (the courier robustness batch, ROADMAP 2026-07-20). The wording
// is load-bearing: it is the only channel through which the player learns the fate of a letter's recipient.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class UndeliverableReasonDescriberTests
    {
        // A dead recipient must be reported as DEAD, not as generically unreachable: the player may hold letters,
        // plans, or feelings for this hero, and the courier's report is how the world tells them.
        [Test]
        public void GIVEN_a_dead_recipient_WHEN_the_failure_is_described_THEN_the_player_learns_of_the_death()
        {
            UndeliverableCause cause = UndeliverableReasonDescriber.Resolve(recipientIsDead: true, recipientIsPrisoner: false);

            cause.Should().Be(UndeliverableCause.RecipientDead);
            UndeliverableReasonDescriber.Describe(cause, "Rhagaea").Should().Be("word comes back that Rhagaea is dead");
        }

        // A captured recipient is a different story from a dead one: the letter failed but the hero lives, and the
        // player may choose to act (ransom, rescue). The report must say captivity, not death or vagueness.
        [Test]
        public void GIVEN_a_captured_recipient_WHEN_the_failure_is_described_THEN_the_player_learns_of_the_captivity()
        {
            UndeliverableCause cause = UndeliverableReasonDescriber.Resolve(recipientIsDead: false, recipientIsPrisoner: true);

            cause.Should().Be(UndeliverableCause.RecipientCaptured);
            UndeliverableReasonDescriber.Describe(cause, "Derthert")
                .Should().Be("Derthert has been taken prisoner, and no letter reaches a captive");
        }

        // Dead outranks captured: a hero who died WHILE a prisoner is reported dead. Reporting captivity for a
        // corpse would invite the player to plan a rescue for someone beyond rescuing.
        [Test]
        public void GIVEN_a_recipient_both_dead_and_flagged_prisoner_WHEN_resolved_THEN_death_wins()
        {
            UndeliverableReasonDescriber.Resolve(recipientIsDead: true, recipientIsPrisoner: true)
                .Should().Be(UndeliverableCause.RecipientDead);
        }

        // The vanished case keeps the original generic wording: when the world offers no better fact, inventing
        // one (a death, a capture) would be a false report the player could act on.
        [Test]
        public void GIVEN_a_vanished_recipient_WHEN_the_failure_is_described_THEN_the_wording_stays_generic()
        {
            UndeliverableCause cause = UndeliverableReasonDescriber.Resolve(recipientIsDead: false, recipientIsPrisoner: false);

            cause.Should().Be(UndeliverableCause.RecipientVanished);
            UndeliverableReasonDescriber.Describe(cause, "Mina").Should().Be("Mina could not be reached");
        }
    }
}
