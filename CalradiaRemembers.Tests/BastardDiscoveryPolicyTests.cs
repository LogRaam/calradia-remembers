// Code written by Gabriel Mailhot, 25/06/2026.
// Documents the discovery curve of a hidden bastard: a secret nobody knows can never leak; each extra knower
// raises the odds it surfaces; a crowd of knowers is capped short of certainty in any single period.
//
// WHY IT MATTERS: BastardBehavior rolls DiscoveryChancePercent(age) every SEASON per concealed bastard; a
// discovery fires the scandal (BastardScandalPolicy) and its Renown/Influence cost. Per the policy's own header
// (decided 2026-06-26, option A), the host keeps NO persisted knower-set, only the bastard's identity and live
// age, so this curve is keyed on age alone: an infant is easy to hide, an older child is seen at court and
// starts to resemble their father. Get the curve wrong and a secret either never realistically surfaces or
// becomes near-certain to leak long before a season has even passed.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Bastards;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardDiscoveryPolicyTests
    {
        // BaseSeasonalPercent is the floor even for a newborn ("a careless word, a midwife's gossip", per the
        // constant's own doc): without it a fresh secret would be perfectly safe, which is unrealistic.
        [Test]
        public void GIVEN_an_infant_WHEN_a_season_passes_THEN_the_risk_is_only_the_floor()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(0)
                .Should().Be(BastardDiscoveryPolicy.BaseSeasonalPercent);
        }

        // Pins the actual per-year formula (base + age*PerYearPercent) an older child at court is seen more and
        // starts to resemble their father, so the risk must genuinely track age, not sit flat.
        [Test]
        public void GIVEN_an_older_child_WHEN_a_season_passes_THEN_the_risk_has_grown_with_age()
        {
            //Arrange — base 3 + 5 years * 2 = 13 %.
            BastardDiscoveryPolicy.DiscoveryChancePercent(5)
                .Should().Be(BastardDiscoveryPolicy.BaseSeasonalPercent + 5 * BastardDiscoveryPolicy.PerYearPercent);
        }

        // A secret must never get SAFER purely by aging: a monotonic curve is the whole premise that keeping a
        // bastard concealed is a race against time, not something that eventually blows over on its own.
        [Test]
        public void GIVEN_each_added_year_WHEN_evaluating_THEN_the_risk_never_falls()
        {
            for (int age = 1; age <= 20; age++)
                BastardDiscoveryPolicy.DiscoveryChancePercent(age)
                    .Should().BeGreaterThanOrEqualTo(BastardDiscoveryPolicy.DiscoveryChancePercent(age - 1));
        }

        // MaxDiscoveryPercent exists so discovery is never a certainty in a single season (per the constant's
        // own doc): without a ceiling, an old-enough child's secret could roll as guaranteed to leak every season.
        [Test]
        public void GIVEN_a_long_kept_secret_WHEN_evaluating_THEN_the_seasonal_risk_is_capped()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(30)
                .Should().Be(BastardDiscoveryPolicy.MaxDiscoveryPercent);
        }

        // Defensive: age is read live from the game each season, so a bad or unset value must degrade to the
        // safe floor rather than underflow into a nonsensical (or negative-magnitude) discovery chance.
        [Test]
        public void GIVEN_a_nonsensical_negative_age_WHEN_evaluating_THEN_it_floors_at_the_base()
        {
            BastardDiscoveryPolicy.DiscoveryChancePercent(-3)
                .Should().Be(BastardDiscoveryPolicy.BaseSeasonalPercent);
        }
    }
}
