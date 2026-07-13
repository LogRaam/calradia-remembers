// Code written by Gabriel Mailhot, 11/07/2026.
// Rescue awareness (v1.30.5): once RescueEligibilityPolicy has confirmed a rescue is real, this policy
// decides how each person in the freed lord's circle reacts. RescueBehavior.ApplyGratitude reads Magnitude
// to move regard and RecordMemory to decide whether a lasting first-person memory is written; ResolveFriends
// reads QualifiesAsFriend to build the (capped) friend list. Self and family both remember the debt; a mere
// friend only warms a little and keeps no memory, so getting either the magnitude or the recording flag
// wrong either invents a memory for someone who was not really involved, or forgets one the story earned.

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
        // The freed lord themselves must get both the strongest regard AND a recorded memory: they lived it
        // and, per RescueBehavior's own line ("I will not forget that you came for me"), it stays with them.
        [Test]
        public void GIVEN_the_rescued_prisoner_themselves_WHEN_resolving_gratitude_THEN_it_is_the_self_magnitude_and_recorded()
        {
            RescueGratitude gratitude = RescueGratitudePolicy.GratitudeFor(RescueTie.RescuedSelf);

            gratitude.Magnitude.Should().Be(RescueGratitudePolicy.SelfRegard);
            gratitude.RecordMemory.Should().BeTrue();
        }

        // Close family also keeps a real memory ("my house owes you a debt"), weaker than the freed lord's
        // own but still recorded, distinct from a friend who only warms without remembering the event.
        [Test]
        public void GIVEN_a_close_family_member_WHEN_resolving_gratitude_THEN_it_is_the_family_magnitude_and_recorded()
        {
            RescueGratitude gratitude = RescueGratitudePolicy.GratitudeFor(RescueTie.Family);

            gratitude.Magnitude.Should().Be(RescueGratitudePolicy.FamilyRegard);
            gratitude.RecordMemory.Should().BeTrue();
        }

        // A mere friend must NOT get a memory: RescueBehavior deliberately passes no memory text for friends,
        // so recording one here would fabricate a first-person recollection of an event they were not part
        // of.
        [Test]
        public void GIVEN_a_friend_WHEN_resolving_gratitude_THEN_it_is_the_friend_magnitude_and_not_recorded()
        {
            RescueGratitude gratitude = RescueGratitudePolicy.GratitudeFor(RescueTie.Friend);

            gratitude.Magnitude.Should().Be(RescueGratitudePolicy.FriendRegard);
            gratitude.RecordMemory.Should().BeFalse();
        }

        // The boundary ResolveFriends filters on: one point short of the threshold and this hero is a mere
        // acquaintance of the rescued lord, not close enough to warm toward the player at all.
        [Test]
        public void GIVEN_a_relation_one_below_the_friend_threshold_WHEN_checking_friendship_THEN_it_does_not_qualify()
        {
            RescueGratitudePolicy.QualifiesAsFriend(RescueGratitudePolicy.FriendRelationThreshold - 1).Should().BeFalse();
        }

        // Paired with the test above, fixes the threshold as INCLUSIVE, so a future retune of
        // FriendRelationThreshold cannot silently move who makes the (capped, top-5) friend list.
        [Test]
        public void GIVEN_a_relation_at_the_friend_threshold_WHEN_checking_friendship_THEN_it_qualifies()
        {
            RescueGratitudePolicy.QualifiesAsFriend(RescueGratitudePolicy.FriendRelationThreshold).Should().BeTrue();
        }
    }
}
