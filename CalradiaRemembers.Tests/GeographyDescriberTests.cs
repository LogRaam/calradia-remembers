// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class GeographyDescriberTests
    {
        [TestCase(100f, 0f, "east")]
        [TestCase(0f, 100f, "north")]
        [TestCase(-100f, 0f, "west")]
        [TestCase(0f, -100f, "south")]
        public void GIVEN_a_cardinal_delta_WHEN_asking_the_direction_THEN_it_names_that_cardinal(float dx, float dy, string expected)
        {
            GeographyDescriber.Direction(dx, dy).Should().Be(expected);
        }

        [TestCase(100f, 100f, "north-east")]
        [TestCase(-100f, 100f, "north-west")]
        [TestCase(-100f, -100f, "south-west")]
        [TestCase(100f, -100f, "south-east")]
        public void GIVEN_a_diagonal_delta_WHEN_asking_the_direction_THEN_it_names_that_ordinal(float dx, float dy, string expected)
        {
            GeographyDescriber.Direction(dx, dy).Should().Be(expected);
        }

        [Test]
        public void GIVEN_a_near_coincident_point_WHEN_asking_the_direction_THEN_it_is_empty()
        {
            // Hypotenuse of (3,4) is 5, under SamePlaceRadius (12), so there is no meaningful direction.
            GeographyDescriber.Direction(3f, 4f).Should().Be("");
        }

        [TestCase(5f, "right here")]
        [TestCase(30f, "a short ride")]
        [TestCase(120f, "a few days' ride")]
        [TestCase(300f, "a long way off")]
        [TestCase(600f, "the far side of Calradia")]
        public void GIVEN_a_map_distance_WHEN_banding_it_THEN_it_reads_as_the_expected_phrase(float distance, string expected)
        {
            GeographyDescriber.DistanceBand(distance).Should().Be(expected);
        }

        [Test]
        public void GIVEN_a_distance_exactly_on_a_boundary_WHEN_banding_it_THEN_it_falls_into_the_next_band()
        {
            // ShortRideMax is the exclusive top of "a short ride", so it lands in the following band.
            GeographyDescriber.DistanceBand(GeographyDescriber.ShortRideMax).Should().NotBe("a short ride");
            GeographyDescriber.DistanceBand(GeographyDescriber.ShortRideMax).Should().Be("a few days' ride");
        }

        [Test]
        public void GIVEN_the_same_spot_WHEN_asking_the_bearing_THEN_it_is_right_here()
        {
            GeographyDescriber.Bearing(3f, 4f, 5f).Should().Be("right here");
        }

        [Test]
        public void GIVEN_a_mid_range_north_east_target_WHEN_asking_the_bearing_THEN_it_combines_band_and_direction()
        {
            GeographyDescriber.Bearing(100f, 100f, 141f).Should().Be("a few days' ride to the north-east");
        }
    }
}
