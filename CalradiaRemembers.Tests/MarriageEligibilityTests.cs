// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the marriage eligibility rule shared by the prompt listing, the bridge's marry
// re-validation, and love-match eligibility. The adult-age line and the opposite-sex requirement
// break in silence: a drift either offers impossible matches or refuses every valid one.
//
// WHY IT MATTERS: three call sites (MarriageProspectResolver's prompt listing, BannerlordGameStateBridge's
// ExecuteMarry re-validation, and EncounterContextBuilder's love-match teaching) all share this one rule so
// they can never drift apart. If it drifted, the prompt could offer a marriage the bridge then refuses to seal
// (a broken proposal), or worse, silently propose an impossible pairing (dead, already wed, same-sex, a child)
// as if it were a real prospect.
//
// 2026-07-21: the age GAP was removed from this rule after a player report (a 40-day courtship that could
// never seal). Marriage is a contract between two adults, so this file now pins that a wide gap is lawful.
// The two age concerns that remain real live elsewhere and have their own tests: fertility in
// ConceptionResolver, and what a house OFFERS unprompted in MarriageProspectPolicy.

#region

using CalradiaRemembers.Logic.Marriage;
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
        public void GIVEN_an_unwed_adult_of_the_opposite_sex_WHEN_checking_THEN_they_may_wed()
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
            MarriageEligibility.IsMarriageable(new MarriageEligibility.MarriageMatchFacts {CandidateAlive = false, CandidateHasSpouse = false, CandidateIsFemale = false, PlayerIsFemale = true, CandidateAge = 30, PlayerAge = 28}).Should().BeFalse();
        }

        // A candidate who already has a spouse must be excluded before the bridge's ExecuteMarry re-validation
        // is even reached; letting the prompt list them as available would set up a proposal that can only end
        // in a confusing in-fiction refusal.
        [Test]
        public void GIVEN_a_candidate_already_wed_WHEN_checking_THEN_they_cannot_wed()
        {
            MarriageEligibility.IsMarriageable(new MarriageEligibility.MarriageMatchFacts {CandidateAlive = true, CandidateHasSpouse = true, CandidateIsFemale = false, PlayerIsFemale = true, CandidateAge = 30, PlayerAge = 28}).Should().BeFalse();
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

        // The rule Gabriel ratified 2026-07-21, and the direct cause of the player report that prompted it: a
        // wide age gap is NOT a bar. A player courted an NPC with grandchildren for 40 days at full regard, and
        // the wedding could never seal because this rule silently refused the pair. Marriage is a contract
        // between two adults; how far apart they are is not the contract's business.
        [Test]
        public void GIVEN_two_adults_decades_apart_WHEN_checking_THEN_the_age_gap_is_no_bar()
        {
            //Arrange: a 60-year-old woman and a 25-year-old man: 35 years apart, and lawful.
            IsMatch(candidateAge: 60, candidateIsFemale: true, playerIsFemale: false, playerAge: 25)
                .Should().BeTrue();
        }

        // The same in the other direction, so no future rewrite can reintroduce the gap on one side only: an
        // asymmetric rule would quietly allow one pairing and refuse its mirror image.
        [Test]
        public void GIVEN_the_player_is_the_far_older_party_WHEN_checking_THEN_they_may_still_wed()
        {
            //Arrange: a 22-year-old candidate and a 58-year-old player.
            IsMatch(candidateAge: 22, candidateIsFemale: true, playerIsFemale: false, playerAge: 58)
                .Should().BeTrue();
        }

        #region private

        private static bool IsMatch(float candidateAge, bool candidateIsFemale, bool playerIsFemale, float playerAge)
            => MarriageEligibility.IsMarriageable(new MarriageEligibility.MarriageMatchFacts {CandidateAlive = true, CandidateHasSpouse = false, CandidateIsFemale = candidateIsFemale, PlayerIsFemale = playerIsFemale, CandidateAge = candidateAge, PlayerAge = playerAge});

        #endregion
    }
}
