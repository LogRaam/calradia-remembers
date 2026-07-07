// Code written by Gabriel Mailhot, 07/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CardinalDirectionPolicyTests
    {
        [Test]
        public void GIVEN_a_point_due_east_WHEN_describing_THEN_it_reads_east()
        {
            CardinalDirectionPolicy.Describe(0, 0, 10, 0).Should().Be("east");
        }

        [Test]
        public void GIVEN_a_point_due_north_WHEN_describing_THEN_it_reads_north()
        {
            CardinalDirectionPolicy.Describe(0, 0, 0, 10).Should().Be("north");
        }

        [Test]
        public void GIVEN_a_point_due_west_WHEN_describing_THEN_it_reads_west()
        {
            CardinalDirectionPolicy.Describe(0, 0, -10, 0).Should().Be("west");
        }

        [Test]
        public void GIVEN_a_point_due_south_WHEN_describing_THEN_it_reads_south()
        {
            CardinalDirectionPolicy.Describe(0, 0, 0, -10).Should().Be("south");
        }

        [Test]
        public void GIVEN_a_point_up_and_to_the_right_WHEN_describing_THEN_it_reads_north_east()
        {
            CardinalDirectionPolicy.Describe(0, 0, 10, 10).Should().Be("north-east");
        }

        [Test]
        public void GIVEN_a_point_down_and_to_the_left_WHEN_describing_THEN_it_reads_south_west()
        {
            CardinalDirectionPolicy.Describe(5, 5, 0, 0).Should().Be("south-west");
        }

        [Test]
        public void GIVEN_two_coincident_points_WHEN_describing_THEN_it_reads_nearby()
        {
            CardinalDirectionPolicy.Describe(3, 3, 3, 3).Should().Be("nearby");
        }

        [Test]
        public void GIVEN_a_bearing_measured_from_an_offset_origin_WHEN_describing_THEN_it_is_relative_to_that_origin()
        {
            // The hideout is north-west of the town even though both have positive coordinates.
            CardinalDirectionPolicy.Describe(100, 100, 90, 110).Should().Be("north-west");
        }
    }
}
