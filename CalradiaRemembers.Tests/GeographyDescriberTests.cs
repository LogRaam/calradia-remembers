// Code written by Gabriel Mailhot, 10/07/2026.
// Grounds what an NPC says about where a place lies in the engine's own map data, not a hand-authored
// lorebook: built after a player report that NPCs invented false geography (a Vlandian border castle
// described as leading toward Empire territory when its pass actually leads to Battania). A lorebook
// would only reproduce that same drift as settlements are renamed or change hands mid-campaign, so this
// reads live positions and distances instead. Feeds EncounterContext.GeographyNote (via
// SettlementContextResolver), rendered verbatim under "THE LAY OF THE LAND, treat as fact" in the prompt,
// so the phrases pinned here are exactly what the LLM is told and repeats to the player.

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
        // Wrong cardinal here is exactly the reported bug: an NPC stating a place lies toward the wrong
        // side of the map. Pins the four primary compass words the 45-degree sector math must land on.
        [TestCase(100f, 0f, "east")]
        [TestCase(0f, 100f, "north")]
        [TestCase(-100f, 0f, "west")]
        [TestCase(0f, -100f, "south")]
        public void GIVEN_a_cardinal_delta_WHEN_asking_the_direction_THEN_it_names_that_cardinal(float dx, float dy, string expected)
        {
            GeographyDescriber.Direction(dx, dy).Should().Be(expected);
        }

        // An off-by-one at a 45-degree sector edge would silently swap two adjacent directions (e.g.
        // north-east read as north), the same wrong-direction failure as the cardinal case above.
        [TestCase(100f, 100f, "north-east")]
        [TestCase(-100f, 100f, "north-west")]
        [TestCase(-100f, -100f, "south-west")]
        [TestCase(100f, -100f, "south-east")]
        public void GIVEN_a_diagonal_delta_WHEN_asking_the_direction_THEN_it_names_that_ordinal(float dx, float dy, string expected)
        {
            GeographyDescriber.Direction(dx, dy).Should().Be(expected);
        }

        // Without this floor, two points a few meters apart (effectively the same spot) would still get
        // a fabricated compass word, the very class of invented geography this feature exists to kill.
        [Test]
        public void GIVEN_a_near_coincident_point_WHEN_asking_the_direction_THEN_it_is_empty()
        {
            // Hypotenuse of (3,4) is 5, under SamePlaceRadius (12), so there is no meaningful direction.
            GeographyDescriber.Direction(3f, 4f).Should().Be("");
        }

        // A wrong band understates or overstates real travel distance (a next-door place called "the far
        // side of Calradia", or the reverse), defeating the whole point of grounding in real distance.
        [TestCase(5f, "right here")]
        [TestCase(30f, "a short ride")]
        [TestCase(120f, "a few days' ride")]
        [TestCase(300f, "a long way off")]
        [TestCase(600f, "the far side of Calradia")]
        public void GIVEN_a_map_distance_WHEN_banding_it_THEN_it_reads_as_the_expected_phrase(float distance, string expected)
        {
            GeographyDescriber.DistanceBand(distance).Should().Be(expected);
        }

        // Pins ShortRideMax as an EXCLUSIVE top edge, so a future retune of the TUNE constants can't
        // silently let the boundary value belong to both bands, or neither.
        [Test]
        public void GIVEN_a_distance_exactly_on_a_boundary_WHEN_banding_it_THEN_it_falls_into_the_next_band()
        {
            // ShortRideMax is the exclusive top of "a short ride", so it lands in the following band.
            GeographyDescriber.DistanceBand(GeographyDescriber.ShortRideMax).Should().NotBe("a short ride");
            GeographyDescriber.DistanceBand(GeographyDescriber.ShortRideMax).Should().Be("a few days' ride");
        }

        // Coincident points must read as "right here", not a malformed band-plus-direction phrase (e.g.
        // "right here to the "), since this string is what actually reaches the LLM's prompt verbatim.
        [Test]
        public void GIVEN_the_same_spot_WHEN_asking_the_bearing_THEN_it_is_right_here()
        {
            GeographyDescriber.Bearing(3f, 4f, 5f).Should().Be("right here");
        }

        // Pins the exact concatenated phrase ("<band> to the <direction>") that ships into the prompt;
        // PromptBuilder never reformats it, so a slip in the join here reaches the LLM untested.
        [Test]
        public void GIVEN_a_mid_range_north_east_target_WHEN_asking_the_bearing_THEN_it_combines_band_and_direction()
        {
            GeographyDescriber.Bearing(100f, 100f, 141f).Should().Be("a few days' ride to the north-east");
        }
    }
}
