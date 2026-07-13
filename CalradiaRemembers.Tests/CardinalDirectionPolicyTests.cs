// Code written by Gabriel Mailhot, 07/07/2026.
// Phrases a bandit-hideout quest's direction hint ("the hideout lies to the north of Pravend"),
// consumed by QuestFactory and ViableQuestScanner as quest.DirectionHint = "{bearing} of {anchor}".
// Distinct twin of GeographyDescriber (which grounds free-form chat geography): this one only names a
// compass sector relative to a fixed anchor point, no distance band, and falls back to "nearby" at the
// anchor itself instead of an empty string. A wrong sector here sends the player looking for the
// hideout in the wrong direction from the very settlement the quest names.

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
        // The zero-degree baseline every other sector is measured from (Math.Atan2); get this wrong and
        // every other direction below is wrong by the same offset.
        [Test]
        public void GIVEN_a_point_due_east_WHEN_describing_THEN_it_reads_east()
        {
            CardinalDirectionPolicy.Describe(0, 0, 10, 0).Should().Be("east");
        }

        // Pins the engine-dependent assumption named in the class doc (+Y is north). If a build ever
        // proves the map convention flipped, this is the one test that would catch it and the one place
        // (Describe's north/south sense) that would need to change.
        [Test]
        public void GIVEN_a_point_due_north_WHEN_describing_THEN_it_reads_north()
        {
            CardinalDirectionPolicy.Describe(0, 0, 0, 10).Should().Be("north");
        }

        // Pairs with the due-east case to pin the full east/west axis, not just its positive half.
        [Test]
        public void GIVEN_a_point_due_west_WHEN_describing_THEN_it_reads_west()
        {
            CardinalDirectionPolicy.Describe(0, 0, -10, 0).Should().Be("west");
        }

        // Pairs with the due-north case to pin the full north/south axis, not just its positive half.
        [Test]
        public void GIVEN_a_point_due_south_WHEN_describing_THEN_it_reads_south()
        {
            CardinalDirectionPolicy.Describe(0, 0, 0, -10).Should().Be("south");
        }

        // Exercises the Math.Round(deg / 45) sector math at a 45-degree diagonal: a rounding slip here
        // would snap a genuinely diagonal hideout to a primary direction instead (north or east).
        [Test]
        public void GIVEN_a_point_up_and_to_the_right_WHEN_describing_THEN_it_reads_north_east()
        {
            CardinalDirectionPolicy.Describe(0, 0, 10, 10).Should().Be("north-east");
        }

        // Same diagonal-rounding concern as the north-east case, on the opposite sector, and from a
        // non-zero origin so it cannot pass by coincidentally treating the origin as the map's zero point.
        [Test]
        public void GIVEN_a_point_down_and_to_the_left_WHEN_describing_THEN_it_reads_south_west()
        {
            CardinalDirectionPolicy.Describe(5, 5, 0, 0).Should().Be("south-west");
        }

        // Without this special case, atan2(0, 0) is degenerate and would silently claim the hideout lies
        // "due east" of itself, misdirecting the player at the one point where the hint matters least
        // (they are already there) and most (a quest bug they would notice and report).
        [Test]
        public void GIVEN_two_coincident_points_WHEN_describing_THEN_it_reads_nearby()
        {
            CardinalDirectionPolicy.Describe(3, 3, 3, 3).Should().Be("nearby");
        }

        // Pins the actual call-site shape (anchor town -> hideout, not the other way round). The
        // zero-origin cases above alone would not catch a swapped from/to at the QuestFactory /
        // ViableQuestScanner call site, which would point the player in the exact opposite direction.
        [Test]
        public void GIVEN_a_bearing_measured_from_an_offset_origin_WHEN_describing_THEN_it_is_relative_to_that_origin()
        {
            // The hideout is north-west of the town even though both have positive coordinates.
            CardinalDirectionPolicy.Describe(100, 100, 90, 110).Should().Be("north-west");
        }
    }
}
