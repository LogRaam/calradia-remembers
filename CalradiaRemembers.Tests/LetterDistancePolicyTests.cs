// Code written by Gabriel Mailhot, 03/07/2026.
// Documents that a spouse's absence letter is a graduated chance driven by distance in travel days: silent
// while the player is near, rising only past halfway across the map, capped so it is never a certainty.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterDistancePolicyTests
    {
        // Production doc: "she does not fret when the player has only stepped outside the walls". A player who
        // is merely nearby, right up to and including the NearDays boundary itself, must draw a flat zero
        // chance, not a small nonzero one that would fire the occasional absurd letter from a spouse he just
        // left.
        [Test]
        public void GIVEN_the_player_is_near_WHEN_computing_the_chance_THEN_no_absence_letter()
        {
            LetterDistancePolicy.AbsenceLetterChancePercent(0).Should().Be(0);
            LetterDistancePolicy.AbsenceLetterChancePercent(3).Should().Be(0);
            LetterDistancePolicy.AbsenceLetterChancePercent(LetterDistancePolicy.NearDays).Should().Be(0);
        }

        // File header: "even at the far edge of the world an absent-spouse letter stays an occasional thing".
        // The ceiling must cap the roll, and MaxChancePercent itself must stay under 100, or a spouse at
        // extreme range would write an absence letter every single time, turning a rare beat into a chore.
        [Test]
        public void GIVEN_the_player_is_at_the_far_edge_WHEN_computing_the_chance_THEN_the_ceiling_but_never_certain()
        {
            LetterDistancePolicy.AbsenceLetterChancePercent(LetterDistancePolicy.FarDays)
                .Should().Be(LetterDistancePolicy.MaxChancePercent);
            LetterDistancePolicy.AbsenceLetterChancePercent(20).Should().Be(LetterDistancePolicy.MaxChancePercent);
            LetterDistancePolicy.MaxChancePercent.Should().BeLessThan(100); // never systematic
        }

        // Pins the rise as genuinely LINEAR between NearDays and FarDays, not some other curve: the midpoint
        // must land at exactly half the ceiling, so a future retune of the endpoints keeps predictable, evenly
        // graduated odds rather than a hidden skew toward one end.
        [Test]
        public void GIVEN_the_player_is_halfway_across_the_band_WHEN_computing_the_chance_THEN_about_half_the_ceiling()
        {
            // Midpoint between NearDays (5) and FarDays (10) is 7.5 travel days.
            LetterDistancePolicy.AbsenceLetterChancePercent(7.5)
                .Should().Be(LetterDistancePolicy.MaxChancePercent / 2);
        }

        // The far side of the NearDays boundary tested above: one day past "near" must already read as
        // genuinely distant enough to give a nonzero chance, so the transition from 0% is not off-by-one.
        [Test]
        public void GIVEN_the_player_is_just_past_near_WHEN_computing_the_chance_THEN_a_small_but_nonzero_chance()
        {
            int chance = LetterDistancePolicy.AbsenceLetterChancePercent(LetterDistancePolicy.NearDays + 1);
            chance.Should().BeGreaterThan(0);
            chance.Should().BeLessThan(LetterDistancePolicy.MaxChancePercent);
        }

        // "The chance rises with the courier's journey in days" (file header): moving farther away must never
        // reduce the odds, or a player who traveled even further from a lonely spouse could paradoxically make
        // her write less.
        [Test]
        public void GIVEN_the_chance_rises_monotonically_WHEN_moving_farther_THEN_it_never_decreases()
        {
            int atSix = LetterDistancePolicy.AbsenceLetterChancePercent(6);
            int atEight = LetterDistancePolicy.AbsenceLetterChancePercent(8);
            atEight.Should().BeGreaterThan(atSix);
        }
    }
}
