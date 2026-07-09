// Code written by Gabriel Mailhot, 08/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ProvideGoodsPolicyTests
    {
        [TestCase("horse", ProvideGoodsPolicy.Horses)]
        [TestCase("Horses", ProvideGoodsPolicy.Horses)]
        [TestCase("war horse", ProvideGoodsPolicy.Horses)]
        [TestCase("cattle", ProvideGoodsPolicy.Livestock)]
        [TestCase("SHEEP", ProvideGoodsPolicy.Livestock)]
        [TestCase("grain", ProvideGoodsPolicy.Grain)]
        [TestCase("food", ProvideGoodsPolicy.Grain)]
        public void GIVEN_a_known_category_word_WHEN_normalizing_THEN_it_maps_to_the_canonical_key(string raw, string expected)
        {
            ProvideGoodsPolicy.Normalize(raw).Should().Be(expected);
        }

        [TestCase("swords")]
        [TestCase("")]
        [TestCase(null)]
        public void GIVEN_an_unsupported_or_empty_category_WHEN_normalizing_THEN_it_returns_null(string raw)
        {
            ProvideGoodsPolicy.Normalize(raw).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_count_below_or_above_the_band_WHEN_clamping_THEN_it_is_held_inside()
        {
            ProvideGoodsPolicy.ClampCount(0).Should().Be(ProvideGoodsPolicy.MinCount);
            ProvideGoodsPolicy.ClampCount(-5).Should().Be(ProvideGoodsPolicy.MinCount);
            ProvideGoodsPolicy.ClampCount(99999).Should().Be(ProvideGoodsPolicy.MaxCount);
            ProvideGoodsPolicy.ClampCount(20).Should().Be(20);
        }

        [Test]
        public void GIVEN_a_tally_at_or_over_the_required_count_WHEN_testing_THEN_it_is_satisfied()
        {
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 15, requiredCount: 15).Should().BeTrue();
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 16, requiredCount: 15).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_tally_short_of_the_count_or_a_zero_requirement_WHEN_testing_THEN_it_is_not_satisfied()
        {
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 14, requiredCount: 15).Should().BeFalse();
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 5, requiredCount: 0).Should().BeFalse();
        }
    }
}
