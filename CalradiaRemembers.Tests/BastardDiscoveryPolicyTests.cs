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
        public void GIVEN_no_one_knows_WHEN_rolling_THEN_the_secret_cannot_leak()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(0).Should().Be(0);
            BastardDiscoveryPolicy.DiscoveryChancePercent(-3).Should().Be(0);
        }

        [Test]
        public void GIVEN_a_single_knower_WHEN_rolling_THEN_the_risk_is_the_base_leak_rate()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(1)
                .Should().Be(BastardDiscoveryPolicy.PerKnowerLeakPercent);
        }

        [Test]
        public void GIVEN_more_knowers_WHEN_rolling_THEN_the_risk_compounds()
        {
            //Arrange — 1-(0.95^n): two knowers ≈ 10 %, five ≈ 23 %.
            BastardDiscoveryPolicy.DiscoveryChancePercent(2).Should().Be(10);
            BastardDiscoveryPolicy.DiscoveryChancePercent(5).Should().Be(23);
        }

        [Test]
        public void GIVEN_each_added_knower_WHEN_rolling_THEN_the_risk_never_falls()
        {
            for (int n = 1; n <= 12; n++)
                BastardDiscoveryPolicy.DiscoveryChancePercent(n)
                    .Should().BeGreaterThanOrEqualTo(BastardDiscoveryPolicy.DiscoveryChancePercent(n - 1));
        }

        [Test]
        public void GIVEN_a_crowd_of_knowers_WHEN_rolling_THEN_exposure_is_capped_short_of_certainty()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(100)
                .Should().Be(BastardDiscoveryPolicy.MaxDiscoveryPercent);
        }
    }
}
