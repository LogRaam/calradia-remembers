// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar: the pity-timer pacing that decides whether a qualifying bandit-party encounter reveals
// the regional nemesis as its leader. Rising chance, guaranteed by a fixed count of qualifying encounters
// so the hunt never goes on forever, but he is never trivially common either.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisRevealPolicyTests
    {
        // ── RevealChance shape ───────────────────────────────────────────────

        [Test]
        public void GIVEN_zero_encounters_since_reveal_WHEN_computing_the_chance_THEN_it_is_exactly_the_base_chance()
        {
            NemesisRevealPolicy.RevealChance(0).Should().Be(NemesisRevealPolicy.BaseRevealChance);
        }

        [Test]
        public void GIVEN_a_negative_encounter_count_WHEN_computing_the_chance_THEN_it_is_clamped_to_the_base_chance()
        {
            // Defensive: the engine should never pass a negative counter, but pacing must not break if it does.
            NemesisRevealPolicy.RevealChance(-5).Should().Be(NemesisRevealPolicy.BaseRevealChance);
        }

        [Test]
        public void GIVEN_the_chance_curve_WHEN_stepping_through_every_encounter_count_THEN_it_never_decreases()
        {
            double previous = NemesisRevealPolicy.RevealChance(0);
            for (int i = 1; i <= NemesisRevealPolicy.GuaranteeAfterEncounters + 3; i++)
            {
                double current = NemesisRevealPolicy.RevealChance(i);
                current.Should().BeGreaterThanOrEqualTo(previous);
                previous = current;
            }
        }

        [Test]
        public void GIVEN_encounters_at_the_guarantee_count_WHEN_computing_the_chance_THEN_it_is_a_certainty()
        {
            NemesisRevealPolicy.RevealChance(NemesisRevealPolicy.GuaranteeAfterEncounters).Should().Be(1.0);
        }

        [Test]
        public void GIVEN_encounters_past_the_guarantee_count_WHEN_computing_the_chance_THEN_it_stays_a_certainty()
        {
            NemesisRevealPolicy.RevealChance(NemesisRevealPolicy.GuaranteeAfterEncounters + 10).Should().Be(1.0);
        }

        [Test]
        public void GIVEN_an_encounter_count_partway_to_the_guarantee_WHEN_computing_the_chance_THEN_it_sits_strictly_between_base_and_certainty()
        {
            int midpoint = NemesisRevealPolicy.GuaranteeAfterEncounters / 2;
            double chance = NemesisRevealPolicy.RevealChance(midpoint);

            chance.Should().BeGreaterThan(NemesisRevealPolicy.BaseRevealChance);
            chance.Should().BeLessThan(1.0);
        }

        // ── ShouldReveal boundaries ───────────────────────────────────────────

        [Test]
        public void GIVEN_a_roll_below_the_chance_WHEN_asking_ShouldReveal_THEN_it_reveals()
        {
            NemesisRevealPolicy.ShouldReveal(0, roll: 0.0).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_roll_exactly_equal_to_the_chance_WHEN_asking_ShouldReveal_THEN_it_does_not_reveal()
        {
            // Strict less-than: a roll equal to the chance is the boundary miss, matching a half-open [0,1) roll convention.
            NemesisRevealPolicy.ShouldReveal(0, roll: NemesisRevealPolicy.BaseRevealChance).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_roll_above_the_chance_WHEN_asking_ShouldReveal_THEN_it_does_not_reveal()
        {
            NemesisRevealPolicy.ShouldReveal(0, roll: 0.99).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_guarantee_is_reached_WHEN_asking_ShouldReveal_with_any_roll_short_of_one_THEN_it_always_reveals()
        {
            NemesisRevealPolicy.ShouldReveal(NemesisRevealPolicy.GuaranteeAfterEncounters, roll: 0.999999).Should().BeTrue();
        }
    }
}
