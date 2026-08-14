// Code written by Gabriel Mailhot, 14/08/2026.
// Protects the match logic behind the fix for "the monarch notified me per letter of a successful siege
// after a few days when I was part of the force that took it": WorldEventFeeders stamps a marker when the
// player fights on the winning attacking side of a siege assault, and this policy decides whether that
// marker still applies to the settlement whose ownership is changing. Too loose and an unrelated later
// recapture of the same place could wrongly read as player participation; too narrow and the real reported
// case (the settlement-owner-changed event landing a tick after the assault) would be missed.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class SiegeParticipationMarkerTests
    {
        // STAKE: the exact reported case, the assault ends and the ownership change lands the same day.
        // Without this, the fix does nothing for the case it was written for.
        [Test]
        public void GIVEN_the_same_settlement_the_same_day_WHEN_matching_THEN_it_matches()
        {
            SiegeParticipationMarker.Matches("town_A", 100, "town_A", 100).Should().BeTrue();
        }

        // STAKE: the marker is set at MapEventEnded but the settlement-owner-changed event can land a tick
        // later; without tolerating one day of drift, the reported bug would still slip through on that
        // ordering alone.
        [Test]
        public void GIVEN_the_same_settlement_one_day_later_WHEN_matching_THEN_it_still_matches()
        {
            SiegeParticipationMarker.Matches("town_A", 100, "town_A", 101).Should().BeTrue();
        }

        // STAKE: a marker that has gone stale must not keep crediting the player for a siege days later,
        // otherwise an unrelated recapture of the same settlement long after could wrongly read as
        // participation.
        [Test]
        public void GIVEN_the_same_settlement_but_the_marker_is_stale_WHEN_matching_THEN_it_does_not_match()
        {
            SiegeParticipationMarker.Matches("town_A", 100, "town_A", 100 + SiegeParticipationMarker.MaxDayAge + 1).Should().BeFalse();
        }

        // STAKE: a marker must never apply to a DIFFERENT settlement: the whole point of naming the
        // settlement on the marker is to keep it from leaking onto an unrelated siege the player also fought.
        [Test]
        public void GIVEN_a_different_settlement_WHEN_matching_THEN_it_does_not_match()
        {
            SiegeParticipationMarker.Matches("town_A", 100, "town_B", 100).Should().BeFalse();
        }

        // STAKE: no marker was ever stamped (the normal case for the vast majority of settlement-owner
        // changes, which the player had no hand in) must read as no participation, not throw or false-match.
        [Test]
        public void GIVEN_a_null_or_empty_marker_WHEN_matching_THEN_it_does_not_match()
        {
            SiegeParticipationMarker.Matches(null, 100, "town_A", 100).Should().BeFalse();
            SiegeParticipationMarker.Matches("", 100, "town_A", 100).Should().BeFalse();
        }

        // STAKE: guards the direction of the day comparison: a marker from the FUTURE relative to "today"
        // should never happen, but must not match if it somehow did (defends the boundary, not just the
        // common case).
        [Test]
        public void GIVEN_a_marker_day_after_today_WHEN_matching_THEN_it_does_not_match()
        {
            SiegeParticipationMarker.Matches("town_A", 101, "town_A", 100).Should().BeFalse();
        }
    }
}
