// Code written by Gabriel Mailhot, 25/06/2026.
// Documents the discovery curve of a hidden bastard: a secret nobody knows can never leak; each extra knower
// raises the odds it surfaces; a crowd of knowers is capped short of certainty in any single period.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardDiscoveryPolicyTests
    {
        [Test]
        public void GIVEN_an_infant_WHEN_a_season_passes_THEN_the_risk_is_only_the_floor()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(0)
                .Should().Be(BastardDiscoveryPolicy.BaseSeasonalPercent);
        }

        [Test]
        public void GIVEN_an_older_child_WHEN_a_season_passes_THEN_the_risk_has_grown_with_age()
        {
            //Arrange — base 3 + 5 years * 2 = 13 %.
            BastardDiscoveryPolicy.DiscoveryChancePercent(5)
                .Should().Be(BastardDiscoveryPolicy.BaseSeasonalPercent + 5 * BastardDiscoveryPolicy.PerYearPercent);
        }

        [Test]
        public void GIVEN_each_added_year_WHEN_evaluating_THEN_the_risk_never_falls()
        {
            for (int age = 1; age <= 20; age++)
                BastardDiscoveryPolicy.DiscoveryChancePercent(age)
                    .Should().BeGreaterThanOrEqualTo(BastardDiscoveryPolicy.DiscoveryChancePercent(age - 1));
        }

        [Test]
        public void GIVEN_a_long_kept_secret_WHEN_evaluating_THEN_the_seasonal_risk_is_capped()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(30)
                .Should().Be(BastardDiscoveryPolicy.MaxDiscoveryPercent);
        }

        [Test]
        public void GIVEN_a_nonsensical_negative_age_WHEN_evaluating_THEN_it_floors_at_the_base()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(-3)
                .Should().Be(BastardDiscoveryPolicy.BaseSeasonalPercent);
        }
    }
}
