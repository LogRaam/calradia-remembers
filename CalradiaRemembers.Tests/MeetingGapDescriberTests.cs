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

        // A negative gap means "never met" or unresolved (the caller has no real count), so it must fail safe to
        // silence rather than surface a nonsensical "-1 days" note; 0 and 1 are the same-day case and equally
        // too fresh to remark on.
        [Test]
        public void GIVEN_an_unknown_or_same_day_gap_WHEN_describing_THEN_nothing_is_surfaced()
        {
            MeetingGapDescriber.Describe(-1).Should().BeNull(); // never met / unknown
            MeetingGapDescriber.Describe(0).Should().BeNull();
            MeetingGapDescriber.Describe(1).Should().BeNull();
        }

        // Pins RemarkThresholdDays as INCLUSIVE: exactly a week must still stay silent, or a future retune that
        // only moved the constant without checking the comparison operator could start surfacing the very
        // "four days since we last spoke" nagging the reported bug was about.
        [Test]
        public void GIVEN_a_gap_at_the_one_week_threshold_WHEN_describing_THEN_it_is_still_not_surfaced()
        {
            MeetingGapDescriber.Describe(MeetingGapDescriber.RemarkThresholdDays).Should().BeNull(); // 7 days
        }

        // The far side of that same boundary: one day past the threshold is the first gap worth mentioning,
        // and below the season band it is phrased as a plain day count.
        [Test]
        public void GIVEN_a_gap_just_past_a_week_WHEN_describing_THEN_it_is_surfaced_in_days()
        {
            MeetingGapDescriber.Describe(8).Should().Be("It has been roughly 8 days since you last saw the player.");
        }

        // Pins the Calradian-calendar phrasing (SeasonDays=21) an NPC would actually use: a singular "a season"
        // rather than "1 seasons", and the day count switches to season units once it crosses into that band.
        [Test]
        public void GIVEN_a_gap_of_a_season_WHEN_describing_THEN_it_is_phrased_in_seasons()
        {
            MeetingGapDescriber.Describe(30).Should().Be("It has been roughly a season since you last saw the player.");
            MeetingGapDescriber.Describe(50).Should().Be("It has been roughly 2 seasons since you last saw the player.");
        }

        // Same singular/plural care at the year band (YearDays=84): a gap of exactly one Calradian year reads
        // as "a year", not the season-based day math bleeding through as an odd number of seasons.
        [Test]
        public void GIVEN_a_gap_of_years_WHEN_describing_THEN_it_is_phrased_in_years()
        {
            MeetingGapDescriber.Describe(84).Should().Be("It has been roughly a year since you last saw the player.");
            MeetingGapDescriber.Describe(200).Should().Be("It has been roughly 2 years since you last saw the player.");
        }

        // The reported bug: wives (and other household) opened conversations convinced they were "getting left
        // behind any time i leave a city", even while married to and travelling with the player. A household bond
        // must NEVER be handed an elapsed-gap note that reads as a fading acquaintance; it gets the "of your
        // house, apart only while you campaign" framing instead. Pinned by its plain wording, never the day math.
        [Test]
        public void GIVEN_a_household_member_WHEN_describing_THEN_the_gap_is_reframed_as_a_shared_house()
        {
            string note = MeetingGapDescriber.Describe(30, isHousehold: true);

            note.Should().Contain("own house");
            note.Should().Contain("never abandoned, forgotten, or left behind");
            note.Should().NotContain("since you last saw the player");
        }

        // The whole point of the reframe is that it does NOT hinge on how long it has been: a wife feels "left
        // behind" the moment the player rides out of town (a fresh gap that would be silent for an acquaintance),
        // so the reassuring household note must still be present at a same-day gap, not just a long one.
        [Test]
        public void GIVEN_a_household_member_with_a_fresh_gap_WHEN_describing_THEN_the_reassurance_is_still_given()
        {
            MeetingGapDescriber.Describe(1, isHousehold: true).Should().NotBeNull();
            MeetingGapDescriber.Describe(0, isHousehold: true).Should()
                               .Be(MeetingGapDescriber.Describe(30, isHousehold: true));
        }

        // A non-household caller must be untouched by the new overload: the default Describe(int) is exactly the
        // isHousehold:false path, so everyone who is not spouse/consort/kin keeps the ordinary day-gated note the
        // rest of this fixture pins.
        [Test]
        public void GIVEN_a_non_household_member_WHEN_describing_THEN_it_matches_the_ordinary_note()
        {
            MeetingGapDescriber.Describe(8, isHousehold: false).Should().Be(MeetingGapDescriber.Describe(8));
            MeetingGapDescriber.Describe(4, isHousehold: false).Should().BeNull();
        }
    }
}
