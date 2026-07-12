// Code written by Gabriel Mailhot, 22/06/2026.
// Guards player-authored character data: traits clamp to the engine band and unset ones are left alone,
// and resolution never hands one hero another's authored personality (id wins; an id-less homonym is
// refused, not guessed).

#region

using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CharacterOverrideTests
    {
        // ── Trait clamping / selection ──────────────────────────────────────

        [Test]
        public void GIVEN_out_of_band_traits_WHEN_clamping_THEN_each_is_pinned_to_minus2_to_plus2()
        {
            var o = new CharacterOverride { Valor = 5, Honor = -9, Mercy = 1 };
            var levels = o.ClampedTraitLevels().ToDictionary(x => x.Trait, x => x.Level);

            levels[AuthoredTrait.Valor].Should().Be(2);
            levels[AuthoredTrait.Honor].Should().Be(-2);
            levels[AuthoredTrait.Mercy].Should().Be(1);
        }

        [Test]
        public void GIVEN_unset_traits_WHEN_listing_THEN_only_the_authored_ones_appear()
        {
            var o = new CharacterOverride { Honor = 2 }; // the rest stay vanilla
            o.ClampedTraitLevels().Select(x => x.Trait).Should().Equal(AuthoredTrait.Honor);
            o.HasTraits.Should().BeTrue();
        }

        [Test]
        public void GIVEN_no_traits_WHEN_asked_THEN_HasTraits_is_false_and_the_list_is_empty()
        {
            var o = new CharacterOverride { Backstory = "A quiet exile." };
            o.HasTraits.Should().BeFalse();
            o.ClampedTraitLevels().Should().BeEmpty();
            o.HasBackstory.Should().BeTrue();
        }

        [Test]
        public void GIVEN_blank_backstory_WHEN_asked_THEN_HasBackstory_is_false()
        {
            new CharacterOverride { Backstory = "   " }.HasBackstory.Should().BeFalse();
        }

        // ── Resolution ──────────────────────────────────────────────────────

        [Test]
        public void GIVEN_an_id_match_WHEN_resolving_THEN_id_wins_even_if_the_name_differs()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Hero = "Someone Else", Id = "lord_1", Honor = 2 },
                new CharacterOverride { Hero = "Derthert", Valor = 1 }
            });

            set.Resolve(heroId: "lord_1", heroName: "Derthert").Hero.Should().Be("Someone Else");
        }

        [Test]
        public void GIVEN_no_id_match_WHEN_resolving_THEN_a_single_name_match_is_used()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Hero = "Derthert", Valor = 1 } });
            set.Resolve(heroId: "lord_99", heroName: "derthert").Should().NotBeNull(); // case-insensitive
        }

        [Test]
        public void GIVEN_no_match_at_all_WHEN_resolving_THEN_it_returns_null()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Hero = "Derthert" } });
            set.Resolve("x", "Caladog").Should().BeNull();
        }

        [Test]
        public void GIVEN_two_entries_with_the_same_name_and_no_id_WHEN_resolving_THEN_it_refuses_rather_than_guess()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Hero = "Aria", Honor = 2 },
                new CharacterOverride { Hero = "Aria", Honor = -2 }
            });

            set.Resolve(heroId: null, heroName: "Aria").Should().BeNull();
        }

        [Test]
        public void GIVEN_homonyms_disambiguated_by_id_WHEN_resolving_THEN_the_right_one_wins()
        {
            var set = new CharacterOverrideSet(new[]
            {
                new CharacterOverride { Hero = "Aria", Id = "aria_north", Honor = 2 },
                new CharacterOverride { Hero = "Aria", Id = "aria_south", Honor = -2 }
            });

            set.Resolve(heroId: "aria_south", heroName: "Aria").Honor.Should().Be(-2);
        }

        // ── Runtime injection (With): the console backstory-test path ───────────

        [Test]
        public void GIVEN_an_injected_override_WHEN_resolving_by_id_THEN_the_injected_backstory_is_returned()
        {
            var set = new CharacterOverrideSet(null)
                .With(new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "A rage-forged warrior." });

            set.Resolve("lord_1", "Derthert").Backstory.Should().Be("A rage-forged warrior.");
        }

        [Test]
        public void GIVEN_an_injection_for_a_hero_that_already_has_a_file_entry_WHEN_resolving_THEN_the_injection_wins_by_id()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "old" } })
                .With(new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "new" });

            set.Resolve("lord_1", "Derthert").Backstory.Should().Be("new");
            set.Count.Should().Be(1); // replaced, not duplicated
        }

        [Test]
        public void GIVEN_an_injection_for_one_hero_WHEN_resolving_others_THEN_the_rest_of_the_set_is_preserved()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "lord_other", Hero = "Caladog", Backstory = "kept" } })
                .With(new CharacterOverride { Id = "lord_1", Hero = "Derthert", Backstory = "injected" });

            set.Resolve("lord_other", "Caladog").Backstory.Should().Be("kept");
            set.Resolve("lord_1", "Derthert").Backstory.Should().Be("injected");
        }

        [Test]
        public void GIVEN_a_null_override_WHEN_added_THEN_the_set_is_unchanged()
        {
            var set = new CharacterOverrideSet(new[] { new CharacterOverride { Id = "lord_1", Hero = "Derthert" } });
            set.With(null).Count.Should().Be(1);
        }
    }
}
