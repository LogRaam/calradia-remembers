// Code written by Gabriel Mailhot, 03/07/2026.
// Documents that a spouse's absence letter is a graduated chance driven by distance in travel days: silent
// while the player is near, rising only past halfway across the map, capped so it is never a certainty.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterDistancePolicyTests
    {
        [Test]
        public void GIVEN_the_player_is_near_WHEN_computing_the_chance_THEN_no_absence_letter()
        {
            LetterDistancePolicy.AbsenceLetterChancePercent(0).Should().Be(0);
            LetterDistancePolicy.AbsenceLetterChancePercent(3).Should().Be(0);
            LetterDistancePolicy.AbsenceLetterChancePercent(LetterDistancePolicy.NearDays).Should().Be(0);
        }

        [Test]
        public void GIVEN_the_player_is_at_the_far_edge_WHEN_computing_the_chance_THEN_the_ceiling_but_never_certain()
        {
            LetterDistancePolicy.AbsenceLetterChancePercent(LetterDistancePolicy.FarDays)
                .Should().Be(LetterDistancePolicy.MaxChancePercent);
            LetterDistancePolicy.AbsenceLetterChancePercent(20).Should().Be(LetterDistancePolicy.MaxChancePercent);
            LetterDistancePolicy.MaxChancePercent.Should().BeLessThan(100); // never systematic
        }

        [Test]
        public void GIVEN_the_player_is_halfway_across_the_band_WHEN_computing_the_chance_THEN_about_half_the_ceiling()
        {
            // Midpoint between NearDays (5) and FarDays (10) is 7.5 travel days.
            LetterDistancePolicy.AbsenceLetterChancePercent(7.5)
                .Should().Be(LetterDistancePolicy.MaxChancePercent / 2);
        }

        [Test]
        public void GIVEN_the_player_is_just_past_near_WHEN_computing_the_chance_THEN_a_small_but_nonzero_chance()
        {
            int chance = LetterDistancePolicy.AbsenceLetterChancePercent(LetterDistancePolicy.NearDays + 1);
            chance.Should().BeGreaterThan(0);
            chance.Should().BeLessThan(LetterDistancePolicy.MaxChancePercent);
        }

        [Test]
        public void GIVEN_the_chance_rises_monotonically_WHEN_moving_farther_THEN_it_never_decreases()
        {
            int atSix = LetterDistancePolicy.AbsenceLetterChancePercent(6);
            int atEight = LetterDistancePolicy.AbsenceLetterChancePercent(8);
            atEight.Should().BeGreaterThan(atSix);
        }
    }
}
