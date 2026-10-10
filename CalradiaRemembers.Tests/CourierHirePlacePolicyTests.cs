// Code written by Gabriel Mailhot, 10/10/2026.
// Where the player may hire a courier. raphareish (Nexus, 10/10/2026): "sending letters from castles and villages?"
// The only door was the town tavern keeper, and castles and villages have no tavern. If this rule is wrong, a castle or
// a village still offers no courier, or a town shows the service twice (its tavern keeper and its menu).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CourierHirePlacePolicyTests
   {
      // raphareish's ask: a castle and a village send letters too, from their own menu.
      [TestCase(CourierHirePlace.Castle)]
      [TestCase(CourierHirePlace.Village)]
      public void GIVEN_a_castle_or_a_village_WHEN_its_menu_opens_THEN_a_courier_can_be_hired_there(CourierHirePlace place)
      {
         CourierHirePlacePolicy.OfferedInTheMenu(place).Should().BeTrue();
      }

      // A town already has its tavern keeper for this; the menu does not offer it a second time. Off a settlement there
      // is no menu to offer it in.
      [TestCase(CourierHirePlace.Town)]
      [TestCase(CourierHirePlace.Elsewhere)]
      public void GIVEN_a_town_or_no_settlement_WHEN_the_menu_is_read_THEN_it_offers_no_courier_of_its_own(CourierHirePlace place)
      {
         CourierHirePlacePolicy.OfferedInTheMenu(place).Should().BeFalse();
      }
   }
}
