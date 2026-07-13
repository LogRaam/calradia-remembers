// Code written by Gabriel Mailhot, 13/07/2026.
// Documents the design call that serving a great house is an HONOUR: the happiness a companion comes to rest at
// is not fixed, it rises with the standing of the clan they serve.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionBaselineTests
    {
        [Test]
        public void GIVEN_a_clan_with_no_standing_WHEN_resting_THEN_the_companion_settles_barely_content()
        {
            CompanionBaseline.From(0).Should().Be(CompanionBaseline.NoStanding); // 58
            CompanionHappiness.Band(CompanionBaseline.From(0)).Should().Be(HappinessBand.Content);
        }

        [Test]
        public void GIVEN_a_great_house_WHEN_resting_THEN_the_companion_settles_DEVOTED_simply_to_belong()
        {
            // The whole point: in a legendary clan, being counted among you is its own reward.
            CompanionBaseline.From(6).Should().Be(CompanionBaseline.Max); // 82
            CompanionHappiness.Band(CompanionBaseline.From(6)).Should().Be(HappinessBand.Devoted);
        }

        [Test]
        public void GIVEN_rising_clan_tiers_WHEN_resting_THEN_each_tier_lifts_the_resting_happiness()
        {
            CompanionBaseline.From(1).Should().BeGreaterThan(CompanionBaseline.From(0));
            CompanionBaseline.From(4).Should().BeGreaterThan(CompanionBaseline.From(3));
        }

        [Test]
        public void GIVEN_an_unexpected_tier_WHEN_resting_THEN_the_baseline_stays_within_sane_bounds()
        {
            // A negative tier, or a tier past vanilla's 6 (a mod), must never produce a nonsense baseline.
            CompanionBaseline.From(-5).Should().Be(CompanionBaseline.Min);
            CompanionBaseline.From(99).Should().Be(CompanionBaseline.Max);
        }
    }
}
