// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RomanticProfilePolicyTests
    {
        // ── BuildKinkPool trait correlations ─────────────────────────────────

        [Test]
        public void GIVEN_neutral_traits_WHEN_building_the_kink_pool_THEN_only_the_base_pool_is_present()
        {
            HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(Neutral());

            pool.Should().BeEquivalentTo(new[] {Kink.Roleplay, Kink.PublicAffection, Kink.Voyeurism, Kink.Exhibitionism});
        }

        [Test]
        public void GIVEN_low_mercy_WHEN_building_the_kink_pool_THEN_it_gains_dominance_and_sadism_leaning_kinks()
        {
            HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Mercy = -1});

            pool.Should().Contain(new[] {
                Kink.Dominance, Kink.Sadism, Kink.BondageGiving, Kink.PowerImbalance,
                Kink.Degradation, Kink.FreeUse, Kink.ImpactPlay, Kink.FearPlay, Kink.Objectification
            });
        }

        [Test]
        public void GIVEN_high_mercy_WHEN_building_the_kink_pool_THEN_it_gains_submission_leaning_kinks()
        {
            HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Mercy = 1});

            pool.Should().Contain(new[] {Kink.Submission, Kink.BondageReceiving, Kink.Masochism, Kink.Praise});
            pool.Should().NotContain(Kink.Dominance);
        }

        [Test]
        public void GIVEN_high_honor_WHEN_building_the_kink_pool_THEN_it_gains_breeding_and_praise()
        {
            HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Honor = 1});

            pool.Should().Contain(new[] {Kink.Breeding, Kink.Praise});
        }

        [Test]
        public void GIVEN_low_honor_WHEN_building_the_kink_pool_THEN_it_gains_power_imbalance_and_corruption()
        {
            HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Honor = -1});

            pool.Should().Contain(new[] {Kink.PowerImbalance, Kink.SwitchTendencies, Kink.CorruptionKink, Kink.Degradation});
        }

        [Test]
        public void GIVEN_high_calculating_WHEN_building_the_kink_pool_THEN_it_gains_control_and_ownership_kinks()
        {
            HashSet<Kink> pool = RomanticProfilePolicy.BuildKinkPool(new RomanticTraits {Calculating = 1});

            pool.Should().Contain(new[] {
                Kink.Dominance, Kink.Possessiveness, Kink.BondageGiving, Kink.OrgasmControl, Kink.Chastity,
                Kink.Objectification, Kink.SensoryDeprivation, Kink.Training, Kink.MasterSlave, Kink.PetPlay
            });
        }

        // ── DetermineOrientation roll bands ──────────────────────────────────

        [Test]
        public void GIVEN_a_roll_under_65_WHEN_determining_orientation_THEN_it_is_heterosexual()
        {
            RomanticProfilePolicy.DetermineOrientation(new FixedRoll(64)).Should().Be(SexualOrientation.Heterosexual);
        }

        [Test]
        public void GIVEN_a_roll_of_65_WHEN_determining_orientation_THEN_it_is_bi_curious()
        {
            RomanticProfilePolicy.DetermineOrientation(new FixedRoll(65)).Should().Be(SexualOrientation.BiCurious);
        }

        [Test]
        public void GIVEN_a_roll_of_89_WHEN_determining_orientation_THEN_it_is_still_bi_curious()
        {
            RomanticProfilePolicy.DetermineOrientation(new FixedRoll(89)).Should().Be(SexualOrientation.BiCurious);
        }

        [Test]
        public void GIVEN_a_roll_of_90_WHEN_determining_orientation_THEN_it_is_bisexual()
        {
            RomanticProfilePolicy.DetermineOrientation(new FixedRoll(90)).Should().Be(SexualOrientation.Bisexual);
        }

        // ── PickDistinct ──────────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_pool_larger_than_the_count_WHEN_picking_distinct_entries_THEN_no_duplicates_are_returned()
        {
            HashSet<Kink> pool = new() {Kink.Roleplay, Kink.PublicAffection, Kink.Voyeurism, Kink.Exhibitionism, Kink.Praise};

            List<Kink> picked = RomanticProfilePolicy.PickDistinct(pool, 3, new Random(1234));

            picked.Should().HaveCount(3);
            picked.Distinct().Should().HaveCount(3);
            picked.Should().OnlyContain(k => pool.Contains(k));
        }

        [Test]
        public void GIVEN_a_count_larger_than_the_pool_WHEN_picking_distinct_entries_THEN_it_is_clamped_to_the_pool_size()
        {
            HashSet<Kink> pool = new() {Kink.Roleplay, Kink.Praise};

            RomanticProfilePolicy.PickDistinct(pool, 10, new Random(1)).Should().HaveCount(2);
        }

        // ── DetermineKinks roll bands (count) ────────────────────────────────

        [Test]
        public void GIVEN_a_roll_under_60_WHEN_determining_kinks_THEN_no_kink_is_rolled()
        {
            RomanticProfilePolicy.DetermineKinks(new FixedRoll(0), Neutral()).Should().BeEmpty();
        }

        [Test]
        public void GIVEN_a_roll_of_96_WHEN_determining_kinks_THEN_two_kinks_are_rolled()
        {
            RomanticProfilePolicy.DetermineKinks(new FixedRoll(96), new RomanticTraits {Mercy = 1}).Should().HaveCount(2);
        }

        [Test]
        public void GIVEN_a_roll_of_97_WHEN_determining_kinks_THEN_three_kinks_are_rolled()
        {
            RomanticProfilePolicy.DetermineKinks(new FixedRoll(97), new RomanticTraits {Mercy = -1}).Should().HaveCount(3);
        }

        // ── Determinism ───────────────────────────────────────────────────────

        [Test]
        public void GIVEN_the_same_seed_WHEN_building_a_full_profile_twice_THEN_every_roll_matches()
        {
            RomanticTraits traits = new() {Honor = 1, Mercy = -1, Valor = 1, Generosity = 0, Calculating = 1};

            SexualOrientation Orientation(Random r) => RomanticProfilePolicy.DetermineOrientation(r);
            List<RomanticPreference> Preferences(Random r) => RomanticProfilePolicy.DeterminePreferences(r, traits);
            List<Kink> Kinks(Random r) => RomanticProfilePolicy.DetermineKinks(r, traits);

            var rngA = new Random(42);
            var rngB = new Random(42);

            Orientation(rngA).Should().Be(Orientation(rngB));
            Preferences(rngA).Should().Equal(Preferences(rngB));
            Kinks(rngA).Should().Equal(Kinks(rngB));
        }

        #region private

        private static RomanticTraits Neutral() => new();

        /// <summary>
        ///   A stub System.Random (Next(int) is virtual) that fixes only the 0..99 percentile roll
        ///   (maxValue == 100) used to pick a band; any other call (e.g. the Fisher-Yates shuffle
        ///   inside PickDistinct, whose maxValue is the pool size) falls through to a real draw so
        ///   it never asks for an out-of-range index.
        /// </summary>
        private sealed class FixedRoll : Random
        {
            private readonly int _roll;

            public FixedRoll(int roll) => _roll = roll;

            public override int Next(int maxValue) => maxValue == 100
                ? _roll
                : base.Next(maxValue);
        }

        #endregion
    }
}
