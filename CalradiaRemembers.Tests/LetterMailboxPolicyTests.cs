// Code written by Gabriel Mailhot, 01/08/2026.
// This is the mailbox scheduler itself: the last gate between "an NPC has a reason to write" and "a letter
// actually reaches the player." It feeds the delivery step directly (whatever SelectNext returns is what
// gets sent); everything upstream (LetterWeights, LetterPacingRules) only matters through this policy. Get
// the budget check wrong and a 100h player with dozens of correspondents drowns in mail; get the ordering or
// aging wrong and the loudest subject never yields the floor, so a patient low-priority letter (or the
// player's own reply-worthy correspondent) waits forever; get diversity wrong and the mailbox becomes a
// monotone feed of whichever reason always scores highest.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterMailboxPolicyTests
    {
        private const int Today = 100;

        private static LetterBudget Budget(int maxPerWindow, int windowDays, int diversityChancePercent)
        {
            return new LetterBudget
            {
                MaxPerWindow = maxPerWindow,
                WindowDays = windowDays,
                DiversityChancePercent = diversityChancePercent
            };
        }

        private static LetterCandidate Candidate(string senderId, int weight, int eligibleSinceDay, LetterReason reason = LetterReason.StanceFavor)
        {
            return new LetterCandidate
            {
                Reason = reason,
                SenderId = senderId,
                Weight = weight,
                EligibleSinceDay = eligibleSinceDay
            };
        }

        // If the window's quota is already spent and SelectNext still hands back a candidate, the budget is
        // decorative: nothing actually caps how much mail a 100h player receives, which is the entire point
        // of this increment.
        [Test]
        public void GIVEN_the_window_has_already_spent_its_quota_WHEN_selecting_THEN_nothing_is_released()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);
            var recentReleases = new List<int> { Today - 1, Today - 3 }; // both inside the 7-day window: 2 already spent
            var candidates = new List<LetterCandidate> { Candidate("lord.a", 90, Today) };

            LetterCandidate result = LetterMailboxPolicy.SelectNext(candidates, recentReleases, Today, budget, diversityRoll: 99);

            result.Should().BeNull();
        }

        // An empty candidate list must resolve to "nothing to send" rather than throwing or returning a
        // placeholder: the caller (nothing eligible today) must be able to call this unconditionally every tick.
        [Test]
        public void GIVEN_no_candidates_are_eligible_WHEN_selecting_THEN_nothing_is_released()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);

            LetterCandidate result = LetterMailboxPolicy.SelectNext(new List<LetterCandidate>(), new List<int>(), Today, budget, diversityRoll: 99);

            result.Should().BeNull();
        }

        // The whole premise of weighted scheduling: when the dice do not favour diversity, the highest-stakes
        // subject must win the one slot, not merely a plausible one. If a 50-weight letter could beat a
        // 90-weight one under an unfavourable roll, "importance governs the mailbox" would not be true.
        [Test]
        public void GIVEN_a_high_weight_and_a_low_weight_candidate_WHEN_the_diversity_roll_misses_THEN_the_higher_weight_wins()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);
            var candidates = new List<LetterCandidate>
            {
                Candidate("lord.high", 90, Today),
                Candidate("lord.low", 50, Today)
            };

            LetterCandidate result = LetterMailboxPolicy.SelectNext(candidates, new List<int>(), Today, budget, diversityRoll: 99);

            result.SenderId.Should().Be("lord.high");
        }

        // Anti-starvation is the whole reason aging exists: without it, a permanently mid-tier correspondent
        // (a friendly lord's goodwill note, say) could be outscored by fresher high-weight letters forever and
        // never once reach the player, no matter how long it waited. Aging must eventually let patience win.
        [Test]
        public void GIVEN_a_low_weight_candidate_has_waited_long_enough_WHEN_selecting_THEN_aging_lets_it_overtake_a_fresher_higher_weight_candidate()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);
            var candidates = new List<LetterCandidate>
            {
                Candidate("lord.fresh", 65, Today), // no aging: effective weight stays 65
                Candidate("lord.patient", 50, Today - 20) // waited the full 20-day aging cap: 50 + 20 = 70
            };

            LetterCandidate result = LetterMailboxPolicy.SelectNext(candidates, new List<int>(), Today, budget, diversityRoll: 99);

            result.SenderId.Should().Be("lord.patient");
        }

        // Diversity exists so the mailbox does not become a monotone feed of one always-winning subject. If a
        // favourable roll never actually surfaced a lower-weight letter, the DiversityChancePercent dial would
        // do nothing at all, despite being exposed as a tunable setting.
        [Test]
        public void GIVEN_the_diversity_roll_hits_WHEN_a_strictly_lower_weight_candidate_exists_THEN_it_is_released_instead_of_the_top_candidate()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);
            var candidates = new List<LetterCandidate>
            {
                Candidate("lord.high", 90, Today),
                Candidate("lord.low", 60, Today)
            };

            LetterCandidate result = LetterMailboxPolicy.SelectNext(candidates, new List<int>(), Today, budget, diversityRoll: 0);

            result.SenderId.Should().Be("lord.low");
        }

        // If every candidate ties on weight, there is nothing "strictly lower" to divert to; diversity must
        // not invent a demotion out of thin air (or, worse, crash) just because the roll favoured it. The top
        // of the deterministic ordering must still win.
        [Test]
        public void GIVEN_every_candidate_shares_the_same_weight_WHEN_the_diversity_roll_hits_THEN_there_is_nothing_lower_to_divert_to_so_the_top_still_wins()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);
            var candidates = new List<LetterCandidate>
            {
                Candidate("lord.b", 60, Today),
                Candidate("lord.a", 60, Today)
            };

            LetterCandidate result = LetterMailboxPolicy.SelectNext(candidates, new List<int>(), Today, budget, diversityRoll: 0);

            // Deterministic tie-break (EligibleSinceDay then SenderId ordinal) makes "lord.a" the top of the order.
            result.SenderId.Should().Be("lord.a");
        }

        // These five reasons are mechanical or solicited (a divorce deadline, a child's upkeep, a companion's
        // final goodbye, a reply to the player's own letter): missing one because a livelier subject won the
        // budget slot would break a promise the player relies on, not merely annoy them. A flavor reason like
        // BattleVictory must NOT be in this set, or a battle congratulation would wrongly bypass the budget too.
        [TestCase(LetterReason.SpouseDivorceDemand)]
        [TestCase(LetterReason.SpouseDivorceEscalation)]
        [TestCase(LetterReason.ChildSupportRequest)]
        [TestCase(LetterReason.CompanionFarewell)]
        [TestCase(LetterReason.PlayerLetterReply)]
        public void GIVEN_a_guaranteed_reason_WHEN_asked_THEN_it_bypasses_the_budget(LetterReason reason)
        {
            LetterMailboxPolicy.BypassesBudget(reason).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_ordinary_flavor_reason_WHEN_asked_THEN_it_does_NOT_bypass_the_budget()
        {
            LetterMailboxPolicy.BypassesBudget(LetterReason.BattleVictory).Should().BeFalse();
        }

        // GatherRecentReleaseDays derives the whole rolling-window budget from persisted SentLetters, so this
        // guard IS the budget: if a lost courier's letter still counted, a waylaid courier (nobody's fault, and
        // nothing the player ever saw) would shrink their real allowance for the window, silently muting a
        // correspondent who did nothing wrong.
        [Test]
        public void GIVEN_a_lost_letter_WHEN_checking_budget_membership_THEN_it_does_NOT_count()
        {
            LetterMailboxPolicy.CountsTowardBudget(LetterReason.RomanticCorrespondence, isLost: true).Should().BeFalse();
        }

        // The mirror image of the BypassesBudget test cases: a guaranteed-bypass reason was never competing
        // for the budget slot in the first place (SelectNext's caller is expected to have filtered it out of
        // the pool already), so it must not be allowed to eat into the window's quota either, or a divorce
        // deadline landing this week would silently cost the player their one ordinary letter for it.
        [TestCase(LetterReason.SpouseDivorceDemand)]
        [TestCase(LetterReason.SpouseDivorceEscalation)]
        [TestCase(LetterReason.ChildSupportRequest)]
        [TestCase(LetterReason.CompanionFarewell)]
        [TestCase(LetterReason.PlayerLetterReply)]
        public void GIVEN_a_guaranteed_bypass_reason_WHEN_checking_budget_membership_THEN_it_does_NOT_count(LetterReason reason)
        {
            LetterMailboxPolicy.CountsTowardBudget(reason, isLost: false).Should().BeFalse();
        }

        // The positive case: an ordinary budgeted letter (romantic longing, a battle congratulation) that
        // actually reached the player is exactly what the window is meant to be counting. Get this wrong and
        // the budget derivation silently always returns zero, letting every tick release unthrottled.
        [TestCase(LetterReason.RomanticCorrespondence)]
        [TestCase(LetterReason.BattleVictory)]
        public void GIVEN_a_delivered_budgeted_letter_WHEN_checking_budget_membership_THEN_it_counts(LetterReason reason)
        {
            LetterMailboxPolicy.CountsTowardBudget(reason, isLost: false).Should().BeTrue();
        }

        // The scheduler must be pure: the same day, the same candidates, the same roll must always produce
        // the same pick. If it did not, the same game state could deliver a different letter on a re-simulated
        // tick, which would make the feature untestable and, worse, unpredictable for Gabriel to tune.
        [Test]
        public void GIVEN_identical_inputs_WHEN_selecting_twice_THEN_the_same_candidate_is_chosen_both_times()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 25);
            var candidates = new List<LetterCandidate>
            {
                Candidate("lord.high", 90, Today),
                Candidate("lord.low", 60, Today)
            };
            var recentReleases = new List<int> { Today - 5 };

            LetterCandidate first = LetterMailboxPolicy.SelectNext(candidates, recentReleases, Today, budget, diversityRoll: 40);
            LetterCandidate second = LetterMailboxPolicy.SelectNext(candidates, recentReleases, Today, budget, diversityRoll: 40);

            first.SenderId.Should().Be(second.SenderId);
        }

        // STAKE: Gabriel's love letters in wartime: the correspondent the player marked as awaited writes before louder
        // news, and the diversity roll never passes them over.
        [Test]
        public void GIVEN_an_awaited_correspondent_among_louder_letters_WHEN_selecting_THEN_the_awaited_letter_comes_first()
        {
            LetterBudget budget = Budget(maxPerWindow: 2, windowDays: 7, diversityChancePercent: 100);
            LetterCandidate beloved = Candidate("lady.awaited", 30, Today);
            beloved.Awaited = true;
            var candidates = new List<LetterCandidate> { Candidate("lord.war", 90, Today - 5), beloved };

            LetterMailboxPolicy.SelectNext(candidates, new List<int>(), Today, budget, diversityRoll: 0).Should().BeSameAs(beloved);
        }

        // STAKE: priority only (Gabriel: "ne fait que donner la priorite"): a spent budget still holds the awaited letter back.
        [Test]
        public void GIVEN_an_awaited_correspondent_and_a_spent_budget_WHEN_selecting_THEN_nothing_is_released()
        {
            LetterBudget budget = Budget(maxPerWindow: 1, windowDays: 7, diversityChancePercent: 0);
            LetterCandidate beloved = Candidate("lady.awaited", 30, Today);
            beloved.Awaited = true;

            LetterMailboxPolicy.SelectNext(new List<LetterCandidate> { beloved }, new List<int> { Today - 1 }, Today, budget, diversityRoll: 99).Should().BeNull();
        }
    }
}
