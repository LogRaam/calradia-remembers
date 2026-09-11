// Code written by Gabriel Mailhot, 07/07/2026.
// The pure decision behind QuestJournalBridge.ReconcileOnSessionLaunch, which runs on every save load and can
// close a player's active quest-journal entries. The rule exists because of a repeatedly-reported bug: a false
// cancel on load destroyed a valid, often already-completed quest ("my quest auto-failed on load"). So Decide
// only ever closes a mirror on POSITIVE proof (a confirmed-dead giver, or a matching quest found terminal); any
// ambiguity (unresolved reference, unloaded profile, id drift after an update) resolves to Keep, because a
// lingering entry is a far smaller harm than a destroyed completed quest. ShouldRestoreMirror runs the healing
// direction: it re-creates a mirror the old bug destroyed while the quest itself survived in the store, without
// ever duplicating an entry that is already open.

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
        // Baseline: the mirror's whole reason to exist is a live matching quest, so this must never be
        // second-guessed into a close.
        [Test]
        public void GIVEN_a_matching_live_quest_WHEN_deciding_THEN_the_mirror_is_kept()
        {
            QuestReconcilePolicy.Decide(new QuestReconcilePolicy.MirrorFacts {GiverConfirmedDead = false, HasLiveMatch = true, HasTerminalMatch = false})
                .Should().Be(MirrorReconcileDecision.Keep);
        }

        [Test]
        public void GIVEN_an_awaiting_reward_quest_that_fails_the_strict_match_WHEN_deciding_THEN_it_is_still_kept_not_cancelled()
        {
            // The reported bug: a completed, ready-to-turn-in quest whose id-match momentarily fails on load.
            // A living giver with no confident match must NEVER be cancelled.
            QuestReconcilePolicy.Decide(new QuestReconcilePolicy.MirrorFacts {GiverConfirmedDead = false, HasLiveMatch = false, HasTerminalMatch = false})
                .Should().Be(MirrorReconcileDecision.Keep);
        }

        // A dead giver can never be reported to or paid, so this is the one case a positive close is safe
        // and expected, not the false-cancel bug this policy exists to prevent.
        [Test]
        public void GIVEN_the_giver_is_confirmed_dead_WHEN_deciding_THEN_the_mirror_is_closed_as_giver_gone()
        {
            QuestReconcilePolicy.Decide(new QuestReconcilePolicy.MirrorFacts {GiverConfirmedDead = true, HasLiveMatch = false, HasTerminalMatch = false})
                .Should().Be(MirrorReconcileDecision.CloseGiverGone);
        }

        [Test]
        public void GIVEN_a_dead_giver_takes_precedence_even_over_a_live_match_WHEN_deciding_THEN_it_closes()
        {
            // A dead giver cannot be reported to, so the quest is untinnable however live its own state looks.
            QuestReconcilePolicy.Decide(new QuestReconcilePolicy.MirrorFacts {GiverConfirmedDead = true, HasLiveMatch = true, HasTerminalMatch = true})
                .Should().Be(MirrorReconcileDecision.CloseGiverGone);
        }

        // The other POSITIVE-proof case: the quest genuinely finished, so closing the mirror here is a
        // safe, proven close, not a guess made on missing or stale data.
        [Test]
        public void GIVEN_a_matching_terminal_quest_WHEN_deciding_THEN_the_mirror_is_closed_as_terminal()
        {
            QuestReconcilePolicy.Decide(new QuestReconcilePolicy.MirrorFacts {GiverConfirmedDead = false, HasLiveMatch = false, HasTerminalMatch = true})
                .Should().Be(MirrorReconcileDecision.CloseTerminal);
        }

        // Live must out-rank terminal: Decide checks hasLiveMatch first, so a quest that is genuinely active
        // again is never wrongly closed just because an old terminal-state duplicate also matches.
        [Test]
        public void GIVEN_both_a_live_and_a_terminal_match_WHEN_deciding_THEN_the_live_one_wins_and_it_is_kept()
        {
            QuestReconcilePolicy.Decide(new QuestReconcilePolicy.MirrorFacts {GiverConfirmedDead = false, HasLiveMatch = true, HasTerminalMatch = true})
                .Should().Be(MirrorReconcileDecision.Keep);
        }

        // ── Restore (the missing-mirror direction: the journal is a projection of the store) ──

        [Test]
        public void GIVEN_a_live_quest_with_a_living_giver_and_no_open_mirror_WHEN_checking_THEN_the_mirror_is_restored()
        {
            // The healing case: the old false-cancel bug destroyed the journal entry while the quest survived.
            QuestReconcilePolicy.ShouldRestoreMirror(new QuestReconcilePolicy.MirrorRestoreFacts {QuestIsLive = true, GiverAliveAndResolved = true, OpenMirrorExists = false})
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_open_mirror_already_shows_the_quest_WHEN_checking_THEN_nothing_is_restored()
        {
            // Idempotence: running the pass twice must not stack duplicate journal entries.
            QuestReconcilePolicy.ShouldRestoreMirror(new QuestReconcilePolicy.MirrorRestoreFacts {QuestIsLive = true, GiverAliveAndResolved = true, OpenMirrorExists = true})
                .Should().BeFalse();
        }

        // A finished quest deserves no mirror at all; restoring one here would resurrect a task the player
        // already saw close out.
        [Test]
        public void GIVEN_a_terminal_quest_WHEN_checking_THEN_no_mirror_is_restored()
        {
            QuestReconcilePolicy.ShouldRestoreMirror(new QuestReconcilePolicy.MirrorRestoreFacts {QuestIsLive = false, GiverAliveAndResolved = true, OpenMirrorExists = false})
                .Should().BeFalse();
        }

        // Restoring blind, without a live resolved giver, would recreate a mirror pointing at a giver who
        // may be dead or simply not yet loaded, the very failure mode Decide's own giver-death check exists
        // to avoid.
        [Test]
        public void GIVEN_a_dead_or_unresolved_giver_WHEN_checking_THEN_no_mirror_is_restored()
        {
            QuestReconcilePolicy.ShouldRestoreMirror(new QuestReconcilePolicy.MirrorRestoreFacts {QuestIsLive = true, GiverAliveAndResolved = false, OpenMirrorExists = false})
                .Should().BeFalse();
        }
    }
}
