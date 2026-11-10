// Code written by Gabriel Mailhot, 10/11/2026.
// What ReturnHomePolicy decides: when a companion riding back from an errand or a patrol rejoins the party. If it is
// wrong, a companion parks beside the player and never rejoins (Aporo, Discord 10/11/2026: "he stands close to me and
// i can't do anything about it only cancel quest"), or one is pulled out of a battle into the party.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ReturnHomePolicyTests
   {
      // STAKE: a companion who reached the player rejoins at once.
      [Test]
      public void GIVEN_a_companion_beside_the_player_WHEN_decided_THEN_they_rejoin()
      {
         ReturnHomePolicy.Decide(new ReturnHomeFacts {Arrived = true}).Should().Be(ReturnHomeStep.Rejoin);
      }

      // STAKE (Aporo): a companion parked just out of reach does not wait for good; after the grace days they rejoin.
      [Test]
      public void GIVEN_a_companion_on_the_road_home_for_the_grace_days_WHEN_not_arrived_THEN_they_rejoin_anyway()
      {
         ReturnHomePolicy.Decide(new ReturnHomeFacts {DaysOnTheRoad = ReturnHomePolicy.GraceDays}).Should().Be(ReturnHomeStep.Rejoin);
         ReturnHomePolicy.Decide(new ReturnHomeFacts {DaysOnTheRoad = ReturnHomePolicy.GraceDays - 1}).Should().Be(ReturnHomeStep.KeepRiding);
      }

      // STAKE: never snatched out of a fight, however long they have been riding.
      [Test]
      public void GIVEN_a_companion_in_battle_WHEN_decided_THEN_they_wait_even_when_arrived_or_long_on_the_road()
      {
         ReturnHomePolicy.Decide(new ReturnHomeFacts {InBattle = true, Arrived = true, DaysOnTheRoad = 10}).Should().Be(ReturnHomeStep.Wait);
      }
   }
}
