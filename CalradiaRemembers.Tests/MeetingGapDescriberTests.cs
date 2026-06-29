// Code written by Gabriel Mailhot, 29/06/2026.
// Documents when the prompt tells an NPC how long it has been since they last saw the player, and how that
// gap is phrased. The flagship case is the reported one: a companion opening with "Four days since we last
// spoke..." — a short gap that should NOT be surfaced at all, because map travel eats days quickly.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MeetingGapDescriberTests
    {
        [Test]
        public void GIVEN_the_reported_four_day_gap_WHEN_describing_THEN_nothing_is_surfaced()
        {
            // "Four days since we last spoke" — too recent to remark on; the player travels fast.
            MeetingGapDescriber.Describe(4).Should().BeNull();
        }

        [Test]
        public void GIVEN_an_unknown_or_same_day_gap_WHEN_describing_THEN_nothing_is_surfaced()
        {
            MeetingGapDescriber.Describe(-1).Should().BeNull(); // never met / unknown
            MeetingGapDescriber.Describe(0).Should().BeNull();
            MeetingGapDescriber.Describe(1).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_gap_at_the_one_week_threshold_WHEN_describing_THEN_it_is_still_not_surfaced()
        {
            MeetingGapDescriber.Describe(MeetingGapDescriber.RemarkThresholdDays).Should().BeNull(); // 7 days
        }

        [Test]
        public void GIVEN_a_gap_just_past_a_week_WHEN_describing_THEN_it_is_surfaced_in_days()
        {
            MeetingGapDescriber.Describe(8).Should().Be("It has been roughly 8 days since you last saw the player.");
        }

        [Test]
        public void GIVEN_a_gap_of_a_season_WHEN_describing_THEN_it_is_phrased_in_seasons()
        {
            MeetingGapDescriber.Describe(30).Should().Be("It has been roughly a season since you last saw the player.");
            MeetingGapDescriber.Describe(50).Should().Be("It has been roughly 2 seasons since you last saw the player.");
        }

        [Test]
        public void GIVEN_a_gap_of_years_WHEN_describing_THEN_it_is_phrased_in_years()
        {
            MeetingGapDescriber.Describe(84).Should().Be("It has been roughly a year since you last saw the player.");
            MeetingGapDescriber.Describe(200).Should().Be("It has been roughly 2 years since you last saw the player.");
        }
    }
}
