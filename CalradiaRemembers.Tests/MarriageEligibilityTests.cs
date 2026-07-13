// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the marriage eligibility rule shared by the prompt listing, the bridge's marry
// re-validation, and love-match eligibility. The age thresholds and the opposite-sex requirement
// break in silence — a drift either offers implausible matches or refuses every valid one.
//
// WHY IT MATTERS: three call sites (MarriageProspectResolver's prompt listing, BannerlordGameStateBridge's
// ExecuteMarry re-validation, and EncounterContextBuilder's love-match teaching) all share this one rule so
// they can never drift apart. If it drifted, the prompt could offer a marriage the bridge then refuses to seal
// (a broken proposal), or worse, silently propose an implausible pairing (dead, already wed, same-sex, decades
// apart) as if it were a real prospect.

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
        // The baseline happy path: a genuinely plausible pairing must pass. Every other test in this file only
        // makes sense as a boundary around this one; if the baseline itself failed, all three consumers
        // (prompt listing, marry re-validation, love-match) would refuse every candidate in the game.
        [Test]
        public void GIVEN_an_unwed_adult_of_the_opposite_sex_within_the_age_gap_WHEN_checking_THEN_they_may_wed()
        {
            //Arrange — a 30-year-old man for a 28-year-old woman.
            IsMatch(candidateAge: 30, candidateIsFemale: false, playerIsFemale: true, playerAge: 28)
                .Should().BeTrue();
        }

        // A dead hero must never be offered as a marriage prospect by the prompt listing; proposing marriage to
        // the deceased is the kind of nonsense a player would report as a bug, not a quirky feature.
        [Test]
        public void GIVEN_a_deceased_candidate_WHEN_checking_THEN_they_cannot_wed()
        {
            MarriageEligibility.IsMarriageable(
                candidateAlive: false, candidateHasSpouse: false, candidateIsFemale: false,
                playerIsFemale: true, candidateAge: 30, playerAge: 28).Should().BeFalse();
        }

        // A candidate who already has a spouse must be excluded before the bridge's ExecuteMarry re-validation
        // is even reached; letting the prompt list them as available would set up a proposal that can only end
        // in a confusing in-fiction refusal.
        [Test]
        public void GIVEN_a_candidate_already_wed_WHEN_checking_THEN_they_cannot_wed()
        {
            MarriageEligibility.IsMarriageable(
                candidateAlive: true, candidateHasSpouse: true, candidateIsFemale: false,
                playerIsFemale: true, candidateAge: 30, playerAge: 28).Should().BeFalse();
        }

        // Vanilla Bannerlord's own marriage action only supports opposite-sex pairs, unlike courtship or
        // romance (which the mod's orientation overrides do allow same-sex). If this rule drifted, the mod
        // would propose a marriage that the underlying engine call cannot actually perform.
        [Test]
        public void GIVEN_a_candidate_of_the_same_sex_WHEN_checking_THEN_they_cannot_wed()
        {
            //Arrange — the engine marries opposite-sex only.
            IsMatch(candidateAge: 30, candidateIsFemale: true, playerIsFemale: true, playerAge: 28)
                .Should().BeFalse();
        }

        // MinMarriageAge pins Bannerlord's own adulthood age (18). A candidate below it must never be offered,
        // the same plausibility floor the mod applies everywhere it deals with adult-only content.
        [Test]
        public void GIVEN_a_candidate_below_marriage_age_WHEN_checking_THEN_they_cannot_wed()
        {
            IsMatch(candidateAge: 17, candidateIsFemale: false, playerIsFemale: true, playerAge: 25)
                .Should().BeFalse();
        }

        // Pins the MinMarriageAge boundary as inclusive: exactly 18 must pass, not just 19+. Getting this edge
        // wrong would refuse the very first day a candidate becomes eligible, an off-by-one a player would only
        // notice as "the game says they're too young" when they aren't.
        [Test]
        public void GIVEN_a_candidate_exactly_at_marriage_age_WHEN_checking_THEN_they_may_wed()
        {
            //Arrange — the minimum age is inclusive.
            IsMatch(candidateAge: MarriageEligibility.MinMarriageAge, candidateIsFemale: false,
                    playerIsFemale: true, playerAge: 30).Should().BeTrue();
        }

        // Pins MaxAgeGap as inclusive: exactly 25 years apart is still a plausible match and must pass. Paired
        // with the next test, this fixes the exact line so a future retune of the constant can't silently move
        // who counts as a believable pairing.
        [Test]
        public void GIVEN_an_age_gap_exactly_at_the_limit_WHEN_checking_THEN_they_may_wed()
        {
            //Arrange — 50 vs 25 is exactly the 25-year cap, inclusive.
            IsMatch(candidateAge: 50, candidateIsFemale: false, playerIsFemale: true, playerAge: 25)
                .Should().BeTrue();
        }

        // The far side of the same boundary: one year past the cap is a grandparent/child-shaped pairing, and
        // must be refused. Without this the prompt listing could offer matches a player would flag as absurd
        // rather than romantic.
        [Test]
        public void GIVEN_an_age_gap_beyond_the_limit_WHEN_checking_THEN_they_cannot_wed()
        {
            //Arrange — 51 vs 25 is a 26-year gap, past the cap (a grandparent/child pairing).
            IsMatch(candidateAge: 51, candidateIsFemale: false, playerIsFemale: true, playerAge: 25)
                .Should().BeFalse();
        }

        // The age gap must be judged the same regardless of who is older. A non-symmetric comparison could pass
        // an older-candidate/younger-player pairing while blocking the reverse at the same gap, silently
        // biasing which direction of age difference the marriage pool allows.
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
