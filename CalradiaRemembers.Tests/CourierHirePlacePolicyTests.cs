// Code written by Gabriel Mailhot, 10/10/2026.
// Where the player may hire a courier. raphareish (Nexus, 10/10/2026): "sending letters from castles and villages?"
// The only door was the town tavern keeper, and castles and villages have no tavern. Kamen (Discord, 23/11/2026) then
// found it in a castle's menu and looked for it in vain in a town's. If this rule is wrong, a castle or a village offers
// no courier, or a town's menu lacks the service its castles' menus show.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CourierHirePlacePolicyTests
   {
      // STAKE: raphareish's ask, a castle and a village send letters too; and Kamen's, the town's menu offers what the
      // castle's does, so the player finds the courier where they found it before (the tavern keeper still offers it).
      [TestCase(CourierHirePlace.Town)]
      [TestCase(CourierHirePlace.Castle)]
      [TestCase(CourierHirePlace.Village)]
      public void GIVEN_a_town_a_castle_or_a_village_WHEN_its_menu_opens_THEN_a_courier_can_be_hired_there(CourierHirePlace place)
      {
         CourierHirePlacePolicy.OfferedInTheMenu(place).Should().BeTrue();
      }

      // STAKE: off a settlement there is no menu to offer it in.
      [Test]
      public void GIVEN_no_settlement_WHEN_the_menu_is_read_THEN_no_courier_is_offered()
      {
         CourierHirePlacePolicy.OfferedInTheMenu(CourierHirePlace.Elsewhere).Should().BeFalse();
      }
   }
}
