// Code written by Gabriel Mailhot, 27/06/2026.
// Documents when a town ambush springs (a roll inside the spring chance) and how many cutthroats a paymaster
// fields (scales with clan tier, bounded so a street ambush never becomes a battle).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AmbushPolicyTests
    {
        [Test]
        public void GIVEN_a_roll_below_the_chance_WHEN_checking_THEN_the_ambush_springs()
        {
            AmbushPolicy.ShouldSpring(0).Should().BeTrue();
            AmbushPolicy.ShouldSpring(AmbushPolicy.SpringChancePercent - 1).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_roll_at_or_above_the_chance_WHEN_checking_THEN_the_streets_stay_quiet()
        {
            AmbushPolicy.ShouldSpring(AmbushPolicy.SpringChancePercent).Should().BeFalse();
            AmbushPolicy.ShouldSpring(99).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_lowly_paymaster_WHEN_counting_attackers_THEN_at_least_a_pair()
        {
            AmbushPolicy.AttackerCount(0).Should().Be(2);
            AmbushPolicy.AttackerCount(-3).Should().Be(2);
        }

        [Test]
        public void GIVEN_a_greater_house_WHEN_counting_attackers_THEN_more_blades_but_bounded()
        {
            AmbushPolicy.AttackerCount(6).Should().Be(5);   // 2 + 6/2
            AmbushPolicy.AttackerCount(100).Should().Be(6); // capped — a street, not a war
        }

        // ── Companions at the player's side (weighted by how many are in the party) ──

        [Test]
        public void GIVEN_no_companions_in_the_party_WHEN_drawing_THEN_the_player_stands_alone()
        {
            AmbushPolicy.CompanionsAtPlayerSide(0, 0).Should().Be(0);
            AmbushPolicy.CompanionsAtPlayerSide(0, 99).Should().Be(0);
        }

        [Test]
        public void GIVEN_a_single_companion_WHEN_drawing_THEN_at_most_one_joins_weighted()
        {
            AmbushPolicy.CompanionsAtPlayerSide(1, 0).Should().Be(1);   // low roll → accompanied
            AmbushPolicy.CompanionsAtPlayerSide(1, 59).Should().Be(1);  // just inside the 60% band
            AmbushPolicy.CompanionsAtPlayerSide(1, 60).Should().Be(0);  // outside → alone
            AmbushPolicy.CompanionsAtPlayerSide(1, 99).Should().Be(0);
        }

        [Test]
        public void GIVEN_two_or_more_companions_WHEN_drawing_THEN_zero_one_or_two_join_capped()
        {
            AmbushPolicy.CompanionsAtPlayerSide(2, 0).Should().Be(2);    // low roll → both
            AmbushPolicy.CompanionsAtPlayerSide(2, 29).Should().Be(2);
            AmbushPolicy.CompanionsAtPlayerSide(2, 30).Should().Be(1);   // middle band → one
            AmbushPolicy.CompanionsAtPlayerSide(2, 79).Should().Be(1);
            AmbushPolicy.CompanionsAtPlayerSide(2, 80).Should().Be(0);   // high roll → alone
            AmbushPolicy.CompanionsAtPlayerSide(5, 0).Should().Be(AmbushPolicy.MaxCompanionsAtSide); // never more than the cap
        }
    }
}
