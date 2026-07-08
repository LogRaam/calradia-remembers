// Code written by Gabriel Mailhot, 07/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestReconcilePolicyTests
    {
        [Test]
        public void GIVEN_a_matching_live_quest_WHEN_deciding_THEN_the_mirror_is_kept()
        {
            QuestReconcilePolicy.Decide(giverConfirmedDead: false, hasLiveMatch: true, hasTerminalMatch: false)
                .Should().Be(MirrorReconcileDecision.Keep);
        }

        [Test]
        public void GIVEN_an_awaiting_reward_quest_that_fails_the_strict_match_WHEN_deciding_THEN_it_is_still_kept_not_cancelled()
        {
            // The reported bug: a completed, ready-to-turn-in quest whose id-match momentarily fails on load.
            // A living giver with no confident match must NEVER be cancelled.
            QuestReconcilePolicy.Decide(giverConfirmedDead: false, hasLiveMatch: false, hasTerminalMatch: false)
                .Should().Be(MirrorReconcileDecision.Keep);
        }

        [Test]
        public void GIVEN_the_giver_is_confirmed_dead_WHEN_deciding_THEN_the_mirror_is_closed_as_giver_gone()
        {
            QuestReconcilePolicy.Decide(giverConfirmedDead: true, hasLiveMatch: false, hasTerminalMatch: false)
                .Should().Be(MirrorReconcileDecision.CloseGiverGone);
        }

        [Test]
        public void GIVEN_a_dead_giver_takes_precedence_even_over_a_live_match_WHEN_deciding_THEN_it_closes()
        {
            // A dead giver cannot be reported to, so the quest is untinnable however live its own state looks.
            QuestReconcilePolicy.Decide(giverConfirmedDead: true, hasLiveMatch: true, hasTerminalMatch: true)
                .Should().Be(MirrorReconcileDecision.CloseGiverGone);
        }

        [Test]
        public void GIVEN_a_matching_terminal_quest_WHEN_deciding_THEN_the_mirror_is_closed_as_terminal()
        {
            QuestReconcilePolicy.Decide(giverConfirmedDead: false, hasLiveMatch: false, hasTerminalMatch: true)
                .Should().Be(MirrorReconcileDecision.CloseTerminal);
        }

        [Test]
        public void GIVEN_both_a_live_and_a_terminal_match_WHEN_deciding_THEN_the_live_one_wins_and_it_is_kept()
        {
            QuestReconcilePolicy.Decide(giverConfirmedDead: false, hasLiveMatch: true, hasTerminalMatch: true)
                .Should().Be(MirrorReconcileDecision.Keep);
        }

        // ── Restore (the missing-mirror direction: the journal is a projection of the store) ──

        [Test]
        public void GIVEN_a_live_quest_with_a_living_giver_and_no_open_mirror_WHEN_checking_THEN_the_mirror_is_restored()
        {
            // The healing case: the old false-cancel bug destroyed the journal entry while the quest survived.
            QuestReconcilePolicy.ShouldRestoreMirror(questIsLive: true, giverAliveAndResolved: true, openMirrorExists: false)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_open_mirror_already_shows_the_quest_WHEN_checking_THEN_nothing_is_restored()
        {
            // Idempotence: running the pass twice must not stack duplicate journal entries.
            QuestReconcilePolicy.ShouldRestoreMirror(questIsLive: true, giverAliveAndResolved: true, openMirrorExists: true)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_terminal_quest_WHEN_checking_THEN_no_mirror_is_restored()
        {
            QuestReconcilePolicy.ShouldRestoreMirror(questIsLive: false, giverAliveAndResolved: true, openMirrorExists: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_dead_or_unresolved_giver_WHEN_checking_THEN_no_mirror_is_restored()
        {
            QuestReconcilePolicy.ShouldRestoreMirror(questIsLive: true, giverAliveAndResolved: false, openMirrorExists: false)
                .Should().BeFalse();
        }
    }
}
