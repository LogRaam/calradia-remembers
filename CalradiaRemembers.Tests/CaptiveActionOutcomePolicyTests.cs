// Code written by Gabriel Mailhot, 20/07/2026.
// Documents who decides a captive player's physical attempt, and how health governs it. The stake is the
// railroad Gabriel play-tested on 2026-07-20: every attempt the player typed was absorbed with no effect
// because the LLM judged its own scene. This policy takes that judgement away from the model, so the verdict
// is real, unbiased, tunable, and testable. It also carries the attrition loop: the captor's harm lowers real
// hit points, so these same numbers quietly strip the player's agency as the scene wears them down.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveActionOutcomePolicyTests
    {
        // A prisoner beaten to nothing still has the floor: without it, a badly wounded player would have
        // literally no agency left and every remaining turn would be pure narration at them.
        [Test]
        public void GIVEN_a_player_at_deaths_door_WHEN_asked_THEN_only_the_floor_chance_remains()
        {
            CaptiveActionOutcomePolicy.SuccessChance(0).Should().Be(CaptiveActionOutcomePolicy.BaseChancePercent);
        }

        // A hale prisoner lands what they try. That is deliberate: succeeding here only means the attempt WORKS
        // and the scene escalates, never that they are freed (escape is its own encounter), so a healthy player
        // getting their attempt is the point, not an imbalance.
        [Test]
        public void GIVEN_a_player_at_full_health_WHEN_asked_THEN_the_attempt_is_certain()
        {
            CaptiveActionOutcomePolicy.SuccessChance(100).Should().Be(100);
            CaptiveActionOutcomePolicy.Succeeds(rollPercent: 99, healthPercent: 100).Should().BeTrue();
        }

        // The attrition loop, which is the whole reason health is the modifier: every wound the captor inflicts
        // (harm_prisoner lowers REAL hit points) must measurably lower the odds of the next attempt.
        [Test]
        public void GIVEN_a_wounded_player_WHEN_asked_THEN_the_chance_falls_with_the_health()
        {
            CaptiveActionOutcomePolicy.SuccessChance(50).Should().Be(60);
            CaptiveActionOutcomePolicy.SuccessChance(20).Should().Be(30);
            CaptiveActionOutcomePolicy.SuccessChance(50)
                .Should().BeLessThan(CaptiveActionOutcomePolicy.SuccessChance(80), "a wound must cost agency");
        }

        // The roll boundary is strict-less-than, so a chance of N means exactly N of the 100 draws succeed.
        // A drifting boundary would silently retune every scene in the game.
        [Test]
        public void GIVEN_a_roll_on_the_boundary_WHEN_resolved_THEN_the_chance_means_exactly_what_it_says()
        {
            CaptiveActionOutcomePolicy.Succeeds(rollPercent: 59, healthPercent: 50).Should().BeTrue();
            CaptiveActionOutcomePolicy.Succeeds(rollPercent: 60, healthPercent: 50).Should().BeFalse();
        }

        // Defensive: a health reading outside 0..100 (an engine oddity, a mod that overheals) must not produce
        // a nonsensical chance or throw. It clamps.
        [Test]
        public void GIVEN_an_out_of_range_health_WHEN_asked_THEN_it_clamps_instead_of_breaking()
        {
            CaptiveActionOutcomePolicy.SuccessChance(-40).Should().Be(CaptiveActionOutcomePolicy.BaseChancePercent);
            CaptiveActionOutcomePolicy.SuccessChance(500).Should().Be(100);
        }
    }
}
