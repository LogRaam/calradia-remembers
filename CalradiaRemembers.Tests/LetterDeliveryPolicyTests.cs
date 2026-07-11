// Code written by Gabriel Mailhot, 11/07/2026.
// Documents the two delivery concerns kept separate: divorce letters always reach the player regardless of
// proximity, while loneliness/longing letters defer only while their sender stands beside the player.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterDeliveryPolicyTests
    {
        [Test]
        public void GIVEN_the_spouse_divorce_demand_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_true()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.SpouseDivorceDemand).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_spouse_divorce_escalation_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_true()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.SpouseDivorceEscalation).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_romantic_correspondence_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_false()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.RomanticCorrespondence).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_spouse_correspondence_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_false()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.SpouseCorrespondence).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_birth_announcement_reason_WHEN_checking_guaranteed_delivery_THEN_it_is_false()
        {
            LetterDeliveryPolicy.IsGuaranteedDelivery(LetterReason.BirthAnnouncement).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_romantic_correspondence_reason_WHEN_checking_defer_when_near_THEN_it_is_true()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.RomanticCorrespondence).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_spouse_correspondence_reason_WHEN_checking_defer_when_near_THEN_it_is_true()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.SpouseCorrespondence).Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_awaiting_reply_reason_WHEN_checking_defer_when_near_THEN_it_is_true()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.AwaitingReply).Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_spouse_divorce_demand_reason_WHEN_checking_defer_when_near_THEN_it_is_false()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.SpouseDivorceDemand).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_tournament_victory_reason_WHEN_checking_defer_when_near_THEN_it_is_false()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.TournamentVictory).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_birth_announcement_reason_WHEN_checking_defer_when_near_THEN_it_is_false()
        {
            LetterDeliveryPolicy.ShouldDeferWhenNear(LetterReason.BirthAnnouncement).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_sender_is_near_WHEN_the_reason_is_romantic_correspondence_THEN_delivery_is_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.RomanticCorrespondence, true).Should().BeTrue();
        }

        [Test]
        public void GIVEN_the_sender_is_near_WHEN_the_reason_is_spouse_divorce_demand_THEN_delivery_is_not_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.SpouseDivorceDemand, true).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_sender_is_far_WHEN_the_reason_is_romantic_correspondence_THEN_delivery_is_not_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.RomanticCorrespondence, false).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_sender_is_near_WHEN_the_reason_is_birth_announcement_THEN_delivery_is_not_deferred()
        {
            LetterDeliveryPolicy.ShouldDeferDelivery(LetterReason.BirthAnnouncement, true).Should().BeFalse();
        }
    }
}
