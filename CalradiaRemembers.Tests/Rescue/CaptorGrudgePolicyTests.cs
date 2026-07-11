// Code written by Gabriel Mailhot, 11/07/2026.

#region

using CalradiaRemembers.Logic.Rescue;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Rescue
{
    [TestFixture]
    public sealed class CaptorGrudgePolicyTests
    {
        [Test]
        public void GIVEN_a_public_action_WHEN_reading_discovery_chance_THEN_it_is_the_public_constant()
        {
            CaptorGrudgePolicy.DiscoveryChance(publicAction: true).Should().Be(CaptorGrudgePolicy.PublicDiscoveryChance);
        }

        [Test]
        public void GIVEN_a_private_action_WHEN_reading_discovery_chance_THEN_it_is_the_private_constant()
        {
            CaptorGrudgePolicy.DiscoveryChance(publicAction: false).Should().Be(CaptorGrudgePolicy.PrivateDiscoveryChance);
        }

        [Test]
        public void GIVEN_a_public_action_and_a_roll_just_below_the_public_threshold_WHEN_checking_discovery_THEN_it_is_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.59, publicAction: true).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_public_action_and_a_roll_at_the_public_threshold_WHEN_checking_discovery_THEN_it_is_not_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.60, publicAction: true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_private_action_and_a_roll_just_below_the_private_threshold_WHEN_checking_discovery_THEN_it_is_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.34, publicAction: false).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_private_action_and_a_roll_at_the_private_threshold_WHEN_checking_discovery_THEN_it_is_not_discovered()
        {
            CaptorGrudgePolicy.Discovers(0.35, publicAction: false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_public_action_WHEN_reading_grudge_magnitude_THEN_it_is_thirty()
        {
            CaptorGrudgePolicy.GrudgeMagnitude(publicAction: true).Should().Be(30);
        }

        [Test]
        public void GIVEN_a_private_action_WHEN_reading_grudge_magnitude_THEN_it_is_twenty()
        {
            CaptorGrudgePolicy.GrudgeMagnitude(publicAction: false).Should().Be(20);
        }

        [Test]
        public void GIVEN_a_public_action_WHEN_checking_if_known_to_player_THEN_it_is_true()
        {
            CaptorGrudgePolicy.KnownToPlayer(publicAction: true).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_private_action_WHEN_checking_if_known_to_player_THEN_it_is_false()
        {
            CaptorGrudgePolicy.KnownToPlayer(publicAction: false).Should().BeFalse();
        }
    }
}
