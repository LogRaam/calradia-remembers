// Code written by Gabriel Mailhot, 08/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisMaterializePolicyTests
    {
        [Test]
        public void GIVEN_a_party_inside_the_region_radius_WHEN_checking_THEN_it_can_be_his()
        {
            NemesisMaterializePolicy.WithinRegion(100f, 100f, 150f, 100f, NemesisMaterializePolicy.RegionMatchRadius)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_party_beyond_the_region_radius_WHEN_checking_THEN_it_is_not_his()
        {
            NemesisMaterializePolicy.WithinRegion(0f, 0f, 1000f, 1000f, NemesisMaterializePolicy.RegionMatchRadius)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_non_positive_radius_WHEN_checking_THEN_it_never_matches()
        {
            NemesisMaterializePolicy.WithinRegion(0f, 0f, 0f, 0f, 0f).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_party_exactly_on_the_anchor_WHEN_checking_THEN_it_matches()
        {
            NemesisMaterializePolicy.WithinRegion(50f, 50f, 50f, 50f, NemesisMaterializePolicy.RegionMatchRadius)
                .Should().BeTrue();
        }
    }
}
