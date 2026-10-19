// Code written by Gabriel Mailhot, 08/08/2026.
// Guards WarLoomingPolicy, the gate deciding whether a pair of kingdoms not yet at war is tense enough to record
// a WarLooming WorldEvent (which then spreads as rumour, "what you've heard", and the chronicle). Its answer feeds
// the weekly world-conditions scan directly: a wrong "yes" spams the realm with phantom war scares, a wrong "no"
// leaves the world silent about a war everyone in Calradia would be bracing for. Each boundary below pins one of
// those failures. Rationale derived from the policy's own header/docs.

#region

using CalradiaRemembers.Logic.War;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class WarLoomingPolicyTests
    {
        // A pair already at war has a real WarDeclared event of its own; announcing "war looms" on top of an
        // ongoing war would be a factual lie the player could verify on the map. The at-war guard must win even
        // when every other signal (high score, old peace, never-recorded) says record.
        [Test]
        public void GIVEN_the_pair_is_already_at_war_WHEN_deciding_THEN_it_is_not_recorded()
        {
            WarLoomingPolicy.ShouldRecord(100.0, alreadyAtWar: true, peaceDays: 500, daysSinceLastLoomingForThisPair: null)
                .Should().BeFalse();
        }

        // Two kingdoms that JUST signed peace still carry a high war-score in the model's eyes; without the peace-age
        // floor a fresh peace would be announced as war-about-to-return the very next weekly pass. One day under the
        // floor must still refuse, pinning exactly where MinPeaceDays sits.
        [Test]
        public void GIVEN_peace_is_fresher_than_the_minimum_WHEN_deciding_THEN_it_is_not_recorded()
        {
            WarLoomingPolicy.ShouldRecord(100.0, alreadyAtWar: false, peaceDays: WarLoomingPolicy.MinPeaceDays - 1, daysSinceLastLoomingForThisPair: null)
                .Should().BeFalse();
        }

        // The floor is INCLUSIVE: peace exactly MinPeaceDays old is old enough to reason about. Fixes the exact edge
        // a future retune of MinPeaceDays must not silently move, so a pair does not get stuck one day short forever.
        [Test]
        public void GIVEN_peace_exactly_at_the_minimum_age_WHEN_all_else_holds_THEN_it_is_recorded()
        {
            WarLoomingPolicy.ShouldRecord(WarLoomingPolicy.WarScoreThreshold, alreadyAtWar: false, peaceDays: WarLoomingPolicy.MinPeaceDays, daysSinceLastLoomingForThisPair: null)
                .Should().BeTrue();
        }

        // Below the war-score threshold the tension is ordinary rivalry, not imminent war; recording it would cry
        // wolf and dull the signal for the pairs that truly are on the brink. Just under the threshold must refuse.
        [Test]
        public void GIVEN_the_war_score_is_below_threshold_WHEN_deciding_THEN_it_is_not_recorded()
        {
            WarLoomingPolicy.ShouldRecord(WarLoomingPolicy.WarScoreThreshold - 0.1, alreadyAtWar: false, peaceDays: 500, daysSinceLastLoomingForThisPair: null)
                .Should().BeFalse();
        }

        // The threshold is INCLUSIVE: a score exactly at the cutoff is tense enough. Pins the boundary so a retune
        // of WarScoreThreshold cannot silently shift which pairs qualify by one point.
        [Test]
        public void GIVEN_the_war_score_exactly_at_threshold_WHEN_all_else_holds_THEN_it_is_recorded()
        {
            WarLoomingPolicy.ShouldRecord(WarLoomingPolicy.WarScoreThreshold, alreadyAtWar: false, peaceDays: 500, daysSinceLastLoomingForThisPair: null)
                .Should().BeTrue();
        }

        // A long-standing cold rivalry would otherwise re-announce itself every weekly pass; the cooldown keeps the
        // rumour network from being flooded by one enduring feud. A pair inside the window must be held back even
        // with a sky-high score.
        [Test]
        public void GIVEN_the_same_pair_loomed_too_recently_WHEN_deciding_THEN_it_is_not_recorded()
        {
            WarLoomingPolicy.ShouldRecord(100.0, alreadyAtWar: false, peaceDays: 500, daysSinceLastLoomingForThisPair: WarLoomingPolicy.RerecordCooldownDays - 1)
                .Should().BeFalse();
        }

        // The cooldown is INCLUSIVE at its edge: once exactly RerecordCooldownDays have passed the pair may loom
        // again. Pins the re-fire boundary so a still-tense rivalry is allowed to resurface on schedule.
        [Test]
        public void GIVEN_the_cooldown_has_exactly_elapsed_WHEN_all_else_holds_THEN_it_is_recorded()
        {
            WarLoomingPolicy.ShouldRecord(100.0, alreadyAtWar: false, peaceDays: 500, daysSinceLastLoomingForThisPair: WarLoomingPolicy.RerecordCooldownDays)
                .Should().BeTrue();
        }

        // A null "days since last" means this pair has NEVER had a looming event; it must be free to record the
        // first one, or the very feature (announcing a brewing war) would never fire on a fresh campaign.
        [Test]
        public void GIVEN_the_pair_has_never_loomed_before_WHEN_all_else_holds_THEN_it_is_recorded()
        {
            WarLoomingPolicy.ShouldRecord(100.0, alreadyAtWar: false, peaceDays: 500, daysSinceLastLoomingForThisPair: null)
                .Should().BeTrue();
        }
    }
}
