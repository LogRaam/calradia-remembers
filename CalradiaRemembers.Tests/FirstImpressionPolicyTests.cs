// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FirstImpressionPolicyTests
    {
        [Test]
        public void GIVEN_a_null_input_WHEN_resolving_THEN_it_is_neutral()
        {
            FirstImpressionPolicy.Resolve(null).Should().Be(FirstImpressionDisposition.Neutral);
        }

        [Test]
        public void GIVEN_an_own_clan_member_with_hostile_traits_WHEN_resolving_THEN_they_open_loyal()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                IsOwnClanMember = true
            };

            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Loyal);
        }

        [Test]
        public void GIVEN_an_already_known_noble_with_hostile_traits_WHEN_resolving_THEN_no_first_impression_colour()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                IsAlreadyKnown = true
            };

            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.None);
        }

        [Test]
        public void GIVEN_all_neutral_inputs_WHEN_resolving_THEN_the_opening_is_neutral()
        {
            var i = new FirstImpressionInputs { Roll = 0 };

            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Neutral);
        }

        [Test]
        public void GIVEN_a_warm_noble_meeting_a_stranger_WHEN_resolving_THEN_they_open_cordial()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = 2,
                Generosity = 2,
                Roll = 0
            };

            // pressure = -2 (mercy) - 2 (generosity) = -4, which is <= -3.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Cordial);
        }

        [Test]
        public void GIVEN_a_cruel_low_honor_noble_vs_a_landless_upstart_WHEN_the_roll_fires_THEN_they_are_demeaning()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                PlayerClanTier = 0,
                PlayerRenownBand = 0,
                PlayerFiefCount = 0,
                Roll = 0
            };

            // pressure = 4 (mercy) + 2 (honor) = 6, chance = min(85, 90) = 85, roll 0 < 85 fires, Mercy<0 wins.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Demeaning);
        }

        [Test]
        public void GIVEN_the_same_cruel_noble_but_a_high_roll_WHEN_resolving_THEN_they_do_not_show_it()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                Honor = -2,
                Roll = 90
            };

            // pressure 6, chance 85, roll 90 >= 85 so this particular noble keeps it to themselves.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Neutral);
        }

        [Test]
        public void GIVEN_an_ambitious_faction_rival_WHEN_the_roll_fires_THEN_they_are_arrogant()
        {
            var i = new FirstImpressionInputs
            {
                Generosity = -2,
                SameFactionRival = true,
                PlayerClanTier = 0,
                Roll = 0
            };

            // pressure = 2 (generosity) + 2 (rival) = 4, chance = 60, roll 0 < 60, stingy rival wins.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Arrogant);
        }

        [Test]
        public void GIVEN_a_low_honor_noble_only_WHEN_the_roll_fires_THEN_they_are_suspicious()
        {
            var i = new FirstImpressionInputs
            {
                Honor = -2,
                PlayerClanTier = 0,
                Roll = 0
            };

            // pressure = 2 (honor), chance = 30, roll 0 < 30, no mercy driver so honor wins.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Suspicious);
        }

        [Test]
        public void GIVEN_high_player_standing_WHEN_resolving_THEN_it_disarms_a_cruel_noble()
        {
            var i = new FirstImpressionInputs
            {
                Mercy = -2,
                PlayerClanTier = 6,
                Roll = 0
            };

            // pressure = 4 (mercy) - 6 (tier) = -2, above 0 but not <= -3, so plainly neutral.
            FirstImpressionPolicy.Resolve(i).Should().Be(FirstImpressionDisposition.Neutral);
        }

        [Test]
        public void GIVEN_the_same_seed_twice_WHEN_taking_a_stable_roll_THEN_the_value_is_identical()
        {
            FirstImpressionPolicy.StableRoll("lord_derthert")
                                 .Should().Be(FirstImpressionPolicy.StableRoll("lord_derthert"));
        }

        [Test]
        public void GIVEN_any_seed_WHEN_taking_a_stable_roll_THEN_it_stays_in_the_zero_to_ninety_nine_range()
        {
            FirstImpressionPolicy.StableRoll("Caladog").Should().BeInRange(0, 99);
            FirstImpressionPolicy.StableRoll("seed").Should().BeInRange(0, 99);
            FirstImpressionPolicy.StableRoll("").Should().BeInRange(0, 99);
        }

        [Test]
        public void GIVEN_a_known_seed_WHEN_taking_a_stable_roll_THEN_it_yields_the_precomputed_value()
        {
            // FNV-1a of "lord_derthert" modulo 100 is a fixed, cross-process value.
            FirstImpressionPolicy.StableRoll("lord_derthert").Should().Be(19);
        }
    }
}
