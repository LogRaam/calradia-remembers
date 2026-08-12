// Code written by Gabriel Mailhot, 11/08/2026.
// EncounterContextBuilder.ResolveMarriageBlockReason consults MarriageBlockOverridePolicy to decide whether a
// SERVED courtship trial may open a match, and CourtshipTrialBehavior consults IsHardBlock to decide whether a
// pairing is even eligible to start a trial. These tests pin the ONE line that must never move: a passed trial
// clears ONLY the soft RegardTooLow gate (the "MARRIAGE NOT YET AN OFFER" the player hit), and can NEVER wave
// away a HARD blocker the game itself forbids marrying across (an active war, a captive on either side, the
// player already wed, the same clan, or an unlawful pairing). If this table drifts, a courtship trial could seal
// a marriage across a war or onto an already-married player, which the whole feature was written to make
// impossible. Every reason is enumerated explicitly so an added or reordered enum value cannot slip through
// unclassified.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MarriageBlockOverridePolicyTests
    {
        // None is not a block at all: the match is already open, so nothing is "hard" about it. Classifying it hard
        // would be harmless here but wrong in meaning, and IsHardBlock is also what gates STARTING a trial (a
        // not-hard-blocked prospect), so None must read as eligible.
        [Test]
        public void GIVEN_none_WHEN_asked_if_hard_THEN_it_is_not_hard()
        {
            MarriageBlockOverridePolicy.IsHardBlock(MarriageBlockReason.None).Should().BeFalse();
        }

        // RegardTooLow is the SINGLE soft gate the trial exists to clear: it is about the prospect's own heart, the
        // one thing riding at the player's side is meant to change. It must read as not hard.
        [Test]
        public void GIVEN_regard_too_low_WHEN_asked_if_hard_THEN_it_is_the_soft_gate_and_not_hard()
        {
            MarriageBlockOverridePolicy.IsHardBlock(MarriageBlockReason.RegardTooLow).Should().BeFalse();
        }

        // The five hard blockers the game itself forbids marrying across. Each is pinned so none can ever be
        // reclassified soft by accident: an active war, a captive, an already-wed player, the same clan, and an
        // unlawful pairing (age/sex/existing marriage) all keep the offer shut no matter what.
        [TestCase(MarriageBlockReason.AtWar)]
        [TestCase(MarriageBlockReason.Captive)]
        [TestCase(MarriageBlockReason.PlayerAlreadyWed)]
        [TestCase(MarriageBlockReason.SameClan)]
        [TestCase(MarriageBlockReason.NotMarriageable)]
        public void GIVEN_a_hard_blocker_WHEN_asked_if_hard_THEN_it_is_hard(MarriageBlockReason reason)
        {
            MarriageBlockOverridePolicy.IsHardBlock(reason).Should().BeTrue();
        }

        // A trial that has NOT been passed overrides nothing, not even the soft gate: only serving the trial earns
        // the override. Without this, a merely-started trial could open the match early.
        [Test]
        public void GIVEN_the_trial_is_not_passed_WHEN_asked_to_override_regard_THEN_it_may_not()
        {
            MarriageBlockOverridePolicy.CanOverride(MarriageBlockReason.RegardTooLow, trialPassed: false).Should().BeFalse();
        }

        // The one override the feature grants: a PASSED trial clears the soft RegardTooLow gate so the match opens.
        // This is the whole point of the mechanism.
        [Test]
        public void GIVEN_the_trial_is_passed_WHEN_asked_to_override_regard_THEN_it_may()
        {
            MarriageBlockOverridePolicy.CanOverride(MarriageBlockReason.RegardTooLow, trialPassed: true).Should().BeTrue();
        }

        // The safety rail, stated over every hard reason at once: even a PASSED trial can never override a hard
        // blocker. This is the assertion that proves a courtship trial can never seal a marriage across a war,
        // onto a captive, onto an already-married player, within the clan, or on an unlawful pairing.
        [TestCase(MarriageBlockReason.AtWar)]
        [TestCase(MarriageBlockReason.Captive)]
        [TestCase(MarriageBlockReason.PlayerAlreadyWed)]
        [TestCase(MarriageBlockReason.SameClan)]
        [TestCase(MarriageBlockReason.NotMarriageable)]
        public void GIVEN_the_trial_is_passed_WHEN_asked_to_override_a_hard_blocker_THEN_it_may_not(MarriageBlockReason reason)
        {
            MarriageBlockOverridePolicy.CanOverride(reason, trialPassed: true).Should().BeFalse();
        }
    }
}
