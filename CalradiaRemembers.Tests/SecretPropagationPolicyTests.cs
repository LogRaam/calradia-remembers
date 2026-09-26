// Code written by Gabriel Mailhot, 05/09/2026.
// SecretPropagationPolicy is the numeric contract for how a held secret-lover identity trickles from a knowing lord
// to his friends (mystery-lover phase 3b-2). It decides who is a friend close enough to be told. Get the floor wrong
// and the secret either never spreads (only bosom friends qualify, so it freezes with the one person told) or spreads
// to every passing acquaintance at once (a broadcast, not a trickle). The daily chance being low is what keeps the
// spread gradual; these pin the friendship boundary that gates who is even eligible to hear it.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SecretPropagationPolicyTests
   {
      // At the floor exactly, a hero is a friend: the boundary is inclusive, so a lord at precisely the friend
      // threshold can be confided in rather than being treated as a stranger by an off-by-one.
      [Test]
      public void GIVEN_relation_at_the_friend_floor_WHEN_checking_THEN_it_is_a_friend()
         => SecretPropagationPolicy.IsFriend(new VanillaRelation(SecretPropagationPolicy.FriendRelationFloor)).Should().BeTrue();

      // Just below the floor is not a friend: an ordinary acquaintance must not be a channel the secret leaks
      // through, or it would spread far too fast to feel like a trusted confidence.
      [Test]
      public void GIVEN_relation_just_below_the_floor_WHEN_checking_THEN_it_is_not_a_friend()
         => SecretPropagationPolicy.IsFriend(new VanillaRelation(SecretPropagationPolicy.FriendRelationFloor - 1)).Should().BeFalse();

      // A cool or hostile relation is never a friend: a lord does not confide a juicy secret to someone he dislikes.
      [Test]
      public void GIVEN_a_negative_relation_WHEN_checking_THEN_it_is_not_a_friend()
         => SecretPropagationPolicy.IsFriend(new VanillaRelation(-30)).Should().BeFalse();
   }
}
