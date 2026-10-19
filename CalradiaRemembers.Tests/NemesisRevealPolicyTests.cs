// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar: the pity-timer pacing that decides whether a qualifying bandit-party encounter reveals
// the regional nemesis as its leader. Rising chance, guaranteed by a fixed count of qualifying encounters
// so the hunt never goes on forever, but he is never trivially common either.

#region

using CalradiaRemembers.Logic.Nemesis;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisRevealPolicyTests
    {
        // ── RevealChance shape ───────────────────────────────────────────────

        // Pins the starting floor of the pity timer: the very first qualifying encounter must sit at the low
        // TUNE base, not already be inflated, or the nemesis would feel common instead of rare-but-building.
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

        // The whole point of a PITY timer is that missing him only ever brings the player closer, never
        // further away; a dip anywhere on the curve would let a string of near-misses reset the player's progress.
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

        // Pins the exact contract promised in the class doc comment: "guaranteed after a fixed number of
        // qualifying misses". If this boundary drifted, "hunt several bands, eventually you run into him"
        // would stop being TRUE by construction, and the hunt could go on forever.
        [Test]
        public void GIVEN_encounters_at_the_guarantee_count_WHEN_computing_the_chance_THEN_it_is_a_certainty()
        {
            NemesisRevealPolicy.RevealChance(NemesisRevealPolicy.GuaranteeAfterEncounters).Should().Be(1.0);
        }

        // The certainty must not decay back down once earned; a player who keeps hunting past the guarantee
        // must never see the odds slip below 100% again.
        [Test]
        public void GIVEN_encounters_past_the_guarantee_count_WHEN_computing_the_chance_THEN_it_stays_a_certainty()
        {
            NemesisRevealPolicy.RevealChance(NemesisRevealPolicy.GuaranteeAfterEncounters + 10).Should().Be(1.0);
        }

        // Confirms the climb is gradual (a real "rising chance"), not a step function that jumps straight
        // from the base to certainty with nothing in between.
        [Test]
        public void GIVEN_an_encounter_count_partway_to_the_guarantee_WHEN_computing_the_chance_THEN_it_sits_strictly_between_base_and_certainty()
        {
            int midpoint = NemesisRevealPolicy.GuaranteeAfterEncounters / 2;
            double chance = NemesisRevealPolicy.RevealChance(midpoint);

            chance.Should().BeGreaterThan(NemesisRevealPolicy.BaseRevealChance);
            chance.Should().BeLessThan(1.0);
        }

        // ── ShouldReveal boundaries ───────────────────────────────────────────

        // Basic contract: the caller's random roll actually drives the reveal, so the pity-timer math above
        // has a real effect on the encounter instead of being computed and ignored.
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

        // A high roll on a low-chance early encounter must still miss, or the base chance printed in the
        // constant would be a lie.
        [Test]
        public void GIVEN_a_roll_above_the_chance_WHEN_asking_ShouldReveal_THEN_it_does_not_reveal()
        {
            NemesisRevealPolicy.ShouldReveal(0, roll: 0.99).Should().BeFalse();
        }

        // At the guarantee, EVERY roll short of 1.0 must reveal, including one this close to the ceiling;
        // this is what makes "guaranteed" an actual certainty rather than just a very high chance.
        [Test]
        public void GIVEN_the_guarantee_is_reached_WHEN_asking_ShouldReveal_with_any_roll_short_of_one_THEN_it_always_reveals()
        {
            NemesisRevealPolicy.ShouldReveal(NemesisRevealPolicy.GuaranteeAfterEncounters, roll: 0.999999).Should().BeTrue();
        }
    }
}
