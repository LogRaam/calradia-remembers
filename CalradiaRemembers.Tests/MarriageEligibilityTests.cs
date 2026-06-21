// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the marriage eligibility rule shared by the prompt listing, the bridge's marry
// re-validation, and love-match eligibility. The age thresholds and the opposite-sex requirement
// break in silence — a drift either offers implausible matches or refuses every valid one.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MarriageEligibilityTests
    {
        [Test]
        public void GIVEN_an_unwed_adult_of_the_opposite_sex_within_the_age_gap_WHEN_checking_THEN_they_may_wed()
        {
            //Arrange — a 30-year-old man for a 28-year-old woman.
            IsMatch(candidateAge: 30, candidateIsFemale: false, playerIsFemale: true, playerAge: 28)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_deceased_candidate_WHEN_checking_THEN_they_cannot_wed()
        {
            MarriageEligibility.IsMarriageable(
                candidateAlive: false, candidateHasSpouse: false, candidateIsFemale: false,
                playerIsFemale: true, candidateAge: 30, playerAge: 28).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_candidate_already_wed_WHEN_checking_THEN_they_cannot_wed()
        {
            MarriageEligibility.IsMarriageable(
                candidateAlive: true, candidateHasSpouse: true, candidateIsFemale: false,
                playerIsFemale: true, candidateAge: 30, playerAge: 28).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_candidate_of_the_same_sex_WHEN_checking_THEN_they_cannot_wed()
        {
            //Arrange — the engine marries opposite-sex only.
            IsMatch(candidateAge: 30, candidateIsFemale: true, playerIsFemale: true, playerAge: 28)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_candidate_below_marriage_age_WHEN_checking_THEN_they_cannot_wed()
        {
            IsMatch(candidateAge: 17, candidateIsFemale: false, playerIsFemale: true, playerAge: 25)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_candidate_exactly_at_marriage_age_WHEN_checking_THEN_they_may_wed()
        {
            //Arrange — the minimum age is inclusive.
            IsMatch(candidateAge: MarriageEligibility.MinMarriageAge, candidateIsFemale: false,
                    playerIsFemale: true, playerAge: 30).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_age_gap_exactly_at_the_limit_WHEN_checking_THEN_they_may_wed()
        {
            //Arrange — 50 vs 25 is exactly the 25-year cap, inclusive.
            IsMatch(candidateAge: 50, candidateIsFemale: false, playerIsFemale: true, playerAge: 25)
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_age_gap_beyond_the_limit_WHEN_checking_THEN_they_cannot_wed()
        {
            //Arrange — 51 vs 25 is a 26-year gap, past the cap (a grandparent/child pairing).
            IsMatch(candidateAge: 51, candidateIsFemale: false, playerIsFemale: true, playerAge: 25)
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_player_is_the_younger_party_within_the_gap_WHEN_checking_THEN_the_gap_is_symmetric()
        {
            //Arrange — a 20-year-old player and a 44-year-old candidate: 24 years, still within the cap.
            IsMatch(candidateAge: 44, candidateIsFemale: true, playerIsFemale: false, playerAge: 20)
                .Should().BeTrue();
        }

        #region private

        private static bool IsMatch(float candidateAge, bool candidateIsFemale, bool playerIsFemale, float playerAge)
            => MarriageEligibility.IsMarriageable(
                candidateAlive: true, candidateHasSpouse: false,
                candidateIsFemale: candidateIsFemale, playerIsFemale: playerIsFemale,
                candidateAge: candidateAge, playerAge: playerAge);

        #endregion
    }
}
