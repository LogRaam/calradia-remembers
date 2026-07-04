// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar: the denar-value-to-magnitude mapping shared by the GiftReceived/GoldReceived
// live plant sites.

#region

using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
    [TestFixture]
    public sealed class AppreciationValueScalingTests
    {
        [Test]
        public void GIVEN_a_middling_value_WHEN_scaling_THEN_it_divides_by_the_denars_per_point_constant()
        {
            AppreciationValueScaling.FromValue(3000).Should().Be(30);
        }

        [Test]
        public void GIVEN_a_tiny_value_WHEN_scaling_THEN_it_floors_at_the_minimum_magnitude()
        {
            AppreciationValueScaling.FromValue(1).Should().Be(AppreciationValueScaling.MinMagnitude);
        }

        [Test]
        public void GIVEN_a_zero_or_negative_value_WHEN_scaling_THEN_it_still_floors_at_the_minimum_magnitude()
        {
            AppreciationValueScaling.FromValue(0).Should().Be(AppreciationValueScaling.MinMagnitude);
            AppreciationValueScaling.FromValue(-500).Should().Be(AppreciationValueScaling.MinMagnitude);
        }

        [Test]
        public void GIVEN_a_huge_value_WHEN_scaling_THEN_it_ceilings_at_the_maximum_magnitude()
        {
            AppreciationValueScaling.FromValue(1000000).Should().Be(AppreciationValueScaling.MaxMagnitude);
        }
    }
}
