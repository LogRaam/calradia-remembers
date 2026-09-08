// Code written by Gabriel Mailhot, 08/09/2026.
// What this pins: how deep a bond a companion needs before they dare confess they have feelings for the player,
// and how that threshold follows the Relationship Pacing the player chose.
//
// Why it exists. Two players said in the same week that their travelling companions felt inert and never engaged
// with them. The romance those companions might eventually have offered was real and already built, but it sat
// behind a regard of 50, which is very far away when ordinary conversation gains are capped at +1 per eight
// game-hours, on a dial that ships at its slowest setting. Reachable in theory, unreachable in practice, which is
// indistinguishable from missing.
//
// Gabriel's ruling, 2026-09-08: rather than add a dial nobody would know how to set (what IS a sensible "minimum
// regard for a confession"?), the threshold follows the question the player has already answered. Relationship
// Pacing describes itself as a philosophy of how readily bonds deepen, and the moment a companion dares to speak
// is exactly that.
//
// If these tests fail, either a Grounded player's campaign has silently shifted under them, or the confession has
// drifted back out of reach for everyone.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionAffectionThresholdTests
    {
        // The promise to existing players: Grounded is the balance the mod shipped with, and it must not move.
        // Someone mid-campaign at regard 45 was not about to be confessed to yesterday and must not be today.
        [Test]
        public void GIVEN_the_grounded_pacing_WHEN_asking_the_threshold_THEN_it_is_exactly_what_shipped()
        {
            CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.Normal)
                .Should().Be(CompanionSecretAffectionPolicy.MinRegard).And.Be(50);
        }

        // The point of the change: a player who asked for a warmer, more story-led game meets the confession at a
        // distance a real campaign covers. Without this the feature stays built and unseen, which is what the two
        // reports were actually describing.
        [Test]
        public void GIVEN_a_warmer_pacing_WHEN_asking_the_threshold_THEN_the_confession_comes_within_reach()
        {
            int grounded = CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.Normal);
            int warm = CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.Easier);
            int storyteller = CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.VeryEasy);

            warm.Should().BeLessThan(grounded);
            storyteller.Should().BeLessThan(warm);
        }

        // Warmer must never mean cheap. Even at Storyteller this is a companion confessing love, so it has to sit
        // well above the regard a few pleasant conversations produce, or the scene stops meaning anything.
        [Test]
        public void GIVEN_the_most_generous_pacing_WHEN_asking_the_threshold_THEN_it_is_still_a_real_bond()
        {
            CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.VeryEasy).Should().BeGreaterThan(20);
        }

        // The eligibility gate must read the SAME threshold, or the dial would move the number and change nothing.
        // A Storyteller companion at 30 is eligible; the same companion on Grounded is not, and both must be true
        // of one policy rather than two that can drift apart.
        [Test]
        public void GIVEN_a_bond_between_the_two_thresholds_WHEN_checking_eligibility_THEN_only_the_warmer_pacing_admits_it()
        {
            const int regard = 30;

            CompanionSecretAffectionPolicy.IsEligible(true, true, true, true, false, regard, RelationWarmth.VeryEasy)
                .Should().BeTrue();

            CompanionSecretAffectionPolicy.IsEligible(true, true, true, true, false, regard, RelationWarmth.Normal)
                .Should().BeFalse();
        }

        // The weekly roll measures from the ACTIVE threshold, so the curve keeps its shape at every pacing: someone
        // who has just cleared the bar confides rarely and grows bolder as the bond deepens. Measuring from the
        // Grounded number instead would hand a Storyteller companion who just qualified a near-zero chance, and
        // the threshold would have moved for nothing.
        [Test]
        public void GIVEN_a_companion_who_just_cleared_the_bar_WHEN_rolling_THEN_the_curve_starts_the_same_everywhere()
        {
            int atGrounded = CompanionSecretAffectionPolicy.WeeklyChancePercent(
                CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.Normal), RelationWarmth.Normal);
            int atStoryteller = CompanionSecretAffectionPolicy.WeeklyChancePercent(
                CompanionSecretAffectionPolicy.MinRegardFor(RelationWarmth.VeryEasy), RelationWarmth.VeryEasy);

            atStoryteller.Should().Be(atGrounded);
        }

        // Nothing below the bar, at any pacing: the confession is the payoff of a bond, never a greeting.
        [Test]
        public void GIVEN_a_bond_below_every_threshold_WHEN_checking_eligibility_THEN_nobody_confides()
        {
            CompanionSecretAffectionPolicy.IsEligible(true, true, true, true, false, 10, RelationWarmth.VeryEasy)
                .Should().BeFalse();
        }
    }
}
