// Code written by Gabriel Mailhot, 11/07/2026.

#region

using CalradiaRemembers.Logic.Rescue;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Rescue
{
    [TestFixture]
    public sealed class RescueGratitudePolicyTests
    {
        [Test]
        public void GIVEN_the_rescued_prisoner_themselves_WHEN_resolving_gratitude_THEN_it_is_the_self_magnitude_and_recorded()
        {
            RescueGratitude gratitude = RescueGratitudePolicy.GratitudeFor(RescueTie.RescuedSelf);

            gratitude.Magnitude.Should().Be(RescueGratitudePolicy.SelfRegard);
            gratitude.RecordMemory.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_close_family_member_WHEN_resolving_gratitude_THEN_it_is_the_family_magnitude_and_recorded()
        {
            RescueGratitude gratitude = RescueGratitudePolicy.GratitudeFor(RescueTie.Family);

            gratitude.Magnitude.Should().Be(RescueGratitudePolicy.FamilyRegard);
            gratitude.RecordMemory.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_friend_WHEN_resolving_gratitude_THEN_it_is_the_friend_magnitude_and_not_recorded()
        {
            RescueGratitude gratitude = RescueGratitudePolicy.GratitudeFor(RescueTie.Friend);

            gratitude.Magnitude.Should().Be(RescueGratitudePolicy.FriendRegard);
            gratitude.RecordMemory.Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_relation_one_below_the_friend_threshold_WHEN_checking_friendship_THEN_it_does_not_qualify()
        {
            RescueGratitudePolicy.QualifiesAsFriend(RescueGratitudePolicy.FriendRelationThreshold - 1).Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_relation_at_the_friend_threshold_WHEN_checking_friendship_THEN_it_qualifies()
        {
            RescueGratitudePolicy.QualifiesAsFriend(RescueGratitudePolicy.FriendRelationThreshold).Should().BeTrue();
        }
    }
}
