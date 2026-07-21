// Code written by Gabriel Mailhot, 21/07/2026.
// Protects the line between two questions that used to be one rule: whether a marriage is LAWFUL
// (MarriageEligibility, no age gap, a contract between two adults) and whether a house would OFFER a given
// kin of its own accord (this policy, which aims young). It feeds MarriageProspectResolver's prompt listing,
// the block that tells a clan head which of their kin they may put forward to the player.
//
// WHY IT MATTERS: collapse the two again and one of two regressions follows. Widen this and a great house
// starts offering the player its sixty-year-old widowed aunt as a political match, which reads as absurd
// rather than dynastic. Narrow the LAWFUL rule to match this one and you restore the exact bug that caused
// the 2026-07-21 player report: a courtship the game silently refused to ever seal.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MarriageProspectPolicyTests
    {
        // The point of the policy, in Gabriel's own terms (ratified 2026-07-21): a spontaneous offer is a
        // political instrument seeking a LONG alliance, so it aims at the young. Without this the offer list
        // is just the legality rule again and the distinction buys nothing.
        [Test]
        public void GIVEN_a_kin_past_the_prospect_age_WHEN_building_an_offer_THEN_the_house_does_not_put_them_forward()
        {
            IsOfferable(candidateAge: MarriageProspectPolicy.MaxProspectAge + 1).Should().BeFalse();
        }

        // The boundary is inclusive, pinned so a retune of the constant cannot quietly shift who is offered by
        // one year in either direction.
        [Test]
        public void GIVEN_a_kin_exactly_at_the_prospect_age_WHEN_building_an_offer_THEN_they_are_still_offered()
        {
            IsOfferable(candidateAge: MarriageProspectPolicy.MaxProspectAge).Should().BeTrue();
        }

        // The other half of the ratified design, and the regression that matters most: narrowing the OFFER
        // must never narrow the LAW. A player who wants the hand of someone older is answered by
        // MarriageEligibility alone, which is what makes the reported 40-day courtship sealable again.
        [Test]
        public void GIVEN_a_kin_past_the_prospect_age_WHEN_the_player_asks_for_them_THEN_the_marriage_is_still_lawful()
        {
            MarriageEligibility.IsMarriageable(
                candidateAlive: true, candidateHasSpouse: false, candidateIsFemale: true,
                playerIsFemale: false, candidateAge: MarriageProspectPolicy.MaxProspectAge + 30,
                playerAge: 25).Should().BeTrue();
        }

        // An offer must still clear every LAWFUL bar first: the policy narrows the pool, it never widens it.
        // Were it to answer on age alone, a house could offer a young kin who is dead or already married.
        [TestCase(false, false, TestName = "GIVEN_a_deceased_kin_WHEN_building_an_offer_THEN_they_are_not_put_forward")]
        [TestCase(true, true, TestName = "GIVEN_an_already_wed_kin_WHEN_building_an_offer_THEN_they_are_not_put_forward")]
        public void GIVEN_a_kin_barred_by_law_WHEN_building_an_offer_THEN_they_are_not_put_forward(
            bool alive, bool hasSpouse)
        {
            MarriageProspectPolicy.IsOfferableProspect(
                candidateAlive: alive, candidateHasSpouse: hasSpouse, candidateIsFemale: true,
                playerIsFemale: false, candidateAge: 22, playerAge: 25).Should().BeFalse();
        }

        // A child is never a prospect, whatever the alliance would be worth. The prospect ceiling must not be
        // read as the ONLY age rule in play here.
        [Test]
        public void GIVEN_a_kin_below_marriage_age_WHEN_building_an_offer_THEN_they_are_not_put_forward()
        {
            IsOfferable(candidateAge: MarriageEligibility.MinMarriageAge - 1).Should().BeFalse();
        }

        #region private

        private static bool IsOfferable(float candidateAge)
            => MarriageProspectPolicy.IsOfferableProspect(
                candidateAlive: true, candidateHasSpouse: false, candidateIsFemale: true,
                playerIsFemale: false, candidateAge: candidateAge, playerAge: 25);

        #endregion
    }
}
