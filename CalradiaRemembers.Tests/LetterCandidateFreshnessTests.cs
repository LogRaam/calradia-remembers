// Code written by Gabriel Mailhot, 01/08/2026.
// This policy decides whether a queued LetterCandidateIntent still deserves to compete for the mailbox, or
// should be quietly retired instead. It feeds LetterTriggerEvaluator.CollectEventCandidates, which drops a
// stale intent from NpcProfile.LetterCandidates before it ever reaches the pool. We do NOT need to deliver
// every letter, only the ones that still matter: a queued congratulation outcompeted for weeks is worse to
// deliver late than to retire quietly, so if this policy calls something fresh when it should not, stale news
// leaks to the player; if it calls something stale too early, a candidate that still mattered is lost forever.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterCandidateFreshnessTests
    {
        // The common case: a candidate enqueued today has had zero days to go stale. If this were ever true,
        // every fresh candidate would be retired on the same tick it was created.
        [Test]
        public void GIVEN_a_candidate_enqueued_today_WHEN_checked_THEN_it_is_fresh()
        {
            LetterCandidateFreshness.IsStale(LetterReason.BattleVictory, eligibleSinceDay: 100, today: 100)
                .Should().BeFalse();
        }

        // The boundary itself: exactly at the window edge must still read fresh, per the design ("strictly
        // greater: exactly at the window edge is still fresh"). Getting this backwards would retire a
        // candidate one tick earlier than the window actually promises.
        [Test]
        public void GIVEN_a_candidate_exactly_at_its_freshness_window_edge_WHEN_checked_THEN_it_is_still_fresh()
        {
            LetterCandidateFreshness.IsStale(
                    LetterReason.BattleVictory,
                    eligibleSinceDay: 100,
                    today: 100 + LetterCandidateFreshness.CongratulationFreshnessDays)
                .Should().BeFalse();
        }

        // One day past the same edge must flip to stale. This is the whole point of the policy: a candidate
        // that has sat unchosen this long is worse delivered late than retired.
        [Test]
        public void GIVEN_a_candidate_one_day_past_its_freshness_window_WHEN_checked_THEN_it_is_stale()
        {
            LetterCandidateFreshness.IsStale(
                    LetterReason.BattleVictory,
                    eligibleSinceDay: 100,
                    today: 100 + LetterCandidateFreshness.CongratulationFreshnessDays + 1)
                .Should().BeTrue();
        }

        // Battle and tournament congratulations share the 15-day window: both are a brag about a moment, worth
        // reading only while it is still recent.
        [TestCase(LetterReason.BattleVictory)]
        [TestCase(LetterReason.TournamentVictory)]
        public void GIVEN_a_congratulation_reason_WHEN_the_window_is_read_THEN_it_is_fifteen_days(LetterReason reason)
        {
            LetterCandidateFreshness.FreshnessDays(reason).Should().Be(LetterCandidateFreshness.CongratulationFreshnessDays);
        }

        // A birth stays meaningful longer than a battle brag, so the same 20-day-old delay must land on
        // opposite sides of freshness depending on the reason: still fresh for a birth, already stale for a
        // battle victory.
        [Test]
        public void GIVEN_a_twenty_day_old_birth_announcement_WHEN_checked_THEN_it_is_still_fresh()
        {
            LetterCandidateFreshness.IsStale(LetterReason.BirthAnnouncement, eligibleSinceDay: 0, today: 20)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_twenty_day_old_battle_victory_WHEN_checked_THEN_it_is_already_stale()
        {
            LetterCandidateFreshness.IsStale(LetterReason.BattleVictory, eligibleSinceDay: 0, today: 20)
                .Should().BeTrue();
        }

        // An unlisted reason must not throw or silently score zero (which would retire it instantly); it falls
        // back to the same default window as every other non-special-cased subject.
        [Test]
        public void GIVEN_a_reason_with_no_dedicated_window_WHEN_the_window_is_read_THEN_it_falls_back_to_the_default()
        {
            LetterCandidateFreshness.FreshnessDays(LetterReason.MarriageProposal)
                .Should().Be(LetterCandidateFreshness.DefaultFreshnessDays);
        }

        // tashmetu (Nexus, 19/09/2026): expelled from his kingdom, still getting letters "as if I were still part of
        // it". A comrade's congratulation queued while the player served with them can wait two weeks for the
        // mailbox; if the player no longer shares the sender's realm by then, it is retired, not delivered.
        [Test]
        public void GIVEN_a_battle_congratulation_from_a_realm_the_player_has_left_WHEN_it_comes_up_THEN_it_no_longer_holds()
        {
            LetterCandidateFreshness.PremiseStillHolds(LetterReason.BattleVictory, senderSharesPlayerRealm: false)
                .Should().BeFalse();
            LetterCandidateFreshness.PremiseStillHolds(LetterReason.BattleVictory, senderSharesPlayerRealm: true)
                .Should().BeTrue();
        }

        // Only the congratulation was queued on the realm; a birth or a tournament never asked who the player serves.
        [Test]
        public void GIVEN_a_letter_that_never_depended_on_the_realm_WHEN_the_player_changes_realm_THEN_it_still_holds()
        {
            LetterCandidateFreshness.PremiseStillHolds(LetterReason.TournamentVictory, senderSharesPlayerRealm: false).Should().BeTrue();
            LetterCandidateFreshness.PremiseStillHolds(LetterReason.BirthAnnouncement, senderSharesPlayerRealm: false).Should().BeTrue();
        }
    }
}
