// Code written by Gabriel Mailhot, 26/06/2026.
// Documents that a discovered bastard cuts deeper in a strictly-monogamous culture and barely stings in a
// permissive one — reusing the jealousy system's per-culture outrage.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardScandalPolicyTests
    {
        [Test]
        public void GIVEN_a_strictly_monogamous_culture_WHEN_pricing_a_scandal_THEN_it_is_grave()
        {
            BastardScandalPolicy.SeverityPercent(JealousyOutrage.High)
                .Should().Be(BastardScandalPolicy.GravePercent);
        }

        [Test]
        public void GIVEN_a_permissive_culture_WHEN_pricing_a_scandal_THEN_it_mostly_shrugs()
        {
            BastardScandalPolicy.SeverityPercent(JealousyOutrage.Low)
                .Should().Be(BastardScandalPolicy.MildPercent);
        }

        [Test]
        public void GIVEN_a_grave_culture_WHEN_scaling_a_cost_THEN_it_is_amplified()
        {
            //Arrange — High outrage at 150 %.
            BastardScandalPolicy.Scale(10, JealousyOutrage.High).Should().Be(15);
        }

        [Test]
        public void GIVEN_a_permissive_culture_WHEN_scaling_a_cost_THEN_it_is_softened()
        {
            BastardScandalPolicy.Scale(10, JealousyOutrage.Low).Should().Be(5);
        }

        [Test]
        public void GIVEN_a_neutral_culture_WHEN_scaling_a_cost_THEN_it_is_unchanged()
        {
            BastardScandalPolicy.Scale(10, JealousyOutrage.Medium).Should().Be(10);
        }

        [Test]
        public void GIVEN_a_positive_base_in_a_permissive_culture_WHEN_scaling_THEN_it_never_vanishes()
        {
            //Arrange — 1 * 50 % rounds to 0, but a scandal always costs at least a little.
            BastardScandalPolicy.Scale(1, JealousyOutrage.Low).Should().Be(1);
        }
    }
}
