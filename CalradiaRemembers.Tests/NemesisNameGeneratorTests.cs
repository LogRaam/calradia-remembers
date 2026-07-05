// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar: deterministic per-culture name/epithet generation. Same seed must always mint the same
// boss (so re-deriving from persisted state never drifts), while different seeds and different band
// cultures should generally diverge — this is first-pass flavour Gabriel can expand later.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisNameGeneratorTests
    {
        [Test]
        public void GIVEN_the_same_culture_and_seed_WHEN_generating_twice_THEN_the_result_is_identical()
        {
            NemesisNameResult first = NemesisNameGenerator.Generate("forest_bandits", 42);
            NemesisNameResult second = NemesisNameGenerator.Generate("forest_bandits", 42);

            first.Name.Should().Be(second.Name);
            first.Epithet.Should().Be(second.Epithet);
        }

        [Test]
        public void GIVEN_an_unknown_band_culture_WHEN_generating_THEN_it_falls_back_to_the_generic_pool_without_throwing()
        {
            NemesisNameResult result = NemesisNameGenerator.Generate("some_modded_culture_key", 7);

            result.Should().NotBeNull();
            result.Name.Should().NotBeNullOrWhiteSpace();
            result.Epithet.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void GIVEN_two_different_seeds_WHEN_generating_for_the_same_culture_THEN_at_least_one_of_them_differs_across_a_spread_of_seeds()
        {
            // Determinism doesn't mean every seed collides — over a spread of seeds we expect variety.
            var names = Enumerable.Range(0, 12)
                .Select(seed => NemesisNameGenerator.Generate("steppe_bandits", seed).Name)
                .Distinct()
                .ToList();

            names.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public void GIVEN_two_different_seeds_WHEN_generating_THEN_the_epithet_can_also_vary_independently_of_the_name()
        {
            var epithets = Enumerable.Range(0, 12)
                .Select(seed => NemesisNameGenerator.Generate("sea_raiders", seed).Epithet)
                .Distinct()
                .ToList();

            epithets.Count.Should().BeGreaterThan(1);
        }

        [TestCase("looters")]
        [TestCase("forest_bandits")]
        [TestCase("mountain_bandits")]
        [TestCase("sea_raiders")]
        [TestCase("desert_bandits")]
        [TestCase("steppe_bandits")]
        public void GIVEN_each_known_band_culture_WHEN_generating_THEN_a_non_empty_name_and_epithet_are_produced(string bandCultureKey)
        {
            NemesisNameResult result = NemesisNameGenerator.Generate(bandCultureKey, 3);

            result.Name.Should().NotBeNullOrWhiteSpace();
            result.Epithet.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void GIVEN_a_culture_WHEN_generating_across_many_seeds_THEN_every_name_comes_from_that_cultures_own_pool_not_another_cultures()
        {
            var steppeNames = Enumerable.Range(0, 20)
                .Select(seed => NemesisNameGenerator.Generate("steppe_bandits", seed).Name)
                .ToList();
            var seaRaiderNames = Enumerable.Range(0, 20)
                .Select(seed => NemesisNameGenerator.Generate("sea_raiders", seed).Name)
                .ToList();

            // The two culture pools are disjoint by construction — no overlap between a steppe boss's
            // name and a sea raider boss's name over a wide seed spread confirms the right pool was used.
            steppeNames.Intersect(seaRaiderNames).Should().BeEmpty();
        }

        [Test]
        public void GIVEN_a_negative_seed_WHEN_generating_THEN_it_still_resolves_to_a_valid_name_without_throwing()
        {
            NemesisNameResult result = NemesisNameGenerator.Generate("looters", -5);

            result.Name.Should().NotBeNullOrWhiteSpace();
            result.Epithet.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void GIVEN_a_result_WHEN_reading_the_full_display_name_THEN_it_joins_name_and_epithet_with_a_space()
        {
            NemesisNameResult result = NemesisNameGenerator.Generate("forest_bandits", 1);

            result.FullName.Should().Be($"{result.Name} {result.Epithet}");
        }
    }
}
