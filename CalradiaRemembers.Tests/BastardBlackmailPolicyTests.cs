// Code written by Gabriel Mailhot, 26/06/2026.
// Documents when a bastard's mother can extort the player and how steep her price is: she has leverage only
// while the secret is hidden, and her demand tracks the player's wealth within a floor-and-ceiling band.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardBlackmailPolicyTests
    {
        [Test]
        public void GIVEN_a_hidden_bastard_and_a_living_unmarried_mother_WHEN_checking_THEN_she_holds_leverage()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: true, motherAlive: true, motherIsPlayerSpouse: false)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_secret_is_already_out_WHEN_checking_THEN_there_is_nothing_left_to_sell()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: false, motherAlive: true, motherIsPlayerSpouse: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_dead_mother_WHEN_checking_THEN_she_holds_no_leverage()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: true, motherAlive: false, motherIsPlayerSpouse: false)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_mother_is_the_players_own_spouse_WHEN_checking_THEN_she_holds_no_leverage()
        {
            BastardBlackmailPolicy.HoldsLeverage(bastardConcealed: true, motherAlive: true, motherIsPlayerSpouse: true)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_poor_player_WHEN_demanding_THEN_the_price_is_floored()
        {
            //Arrange — 10 % of 1000 = 100, below the floor.
            BastardBlackmailPolicy.DemandAmount(1000).Should().Be(BastardBlackmailPolicy.MinDemand);
        }

        [Test]
        public void GIVEN_a_comfortable_player_WHEN_demanding_THEN_she_takes_a_share_of_the_wealth()
        {
            BastardBlackmailPolicy.DemandAmount(50000)
                .Should().Be(50000 * BastardBlackmailPolicy.DemandPercentOfWealth / 100);
        }

        [Test]
        public void GIVEN_a_fabulously_rich_player_WHEN_demanding_THEN_the_price_is_capped()
        {
            BastardBlackmailPolicy.DemandAmount(1_000_000).Should().Be(BastardBlackmailPolicy.MaxDemand);
        }

        [Test]
        public void GIVEN_no_or_negative_gold_WHEN_demanding_THEN_it_floors_at_the_minimum()
        {
            BastardBlackmailPolicy.DemandAmount(0).Should().Be(BastardBlackmailPolicy.MinDemand);
            BastardBlackmailPolicy.DemandAmount(-5000).Should().Be(BastardBlackmailPolicy.MinDemand);
        }
    }
}
