// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A: where a duel is staged, or that it cannot be staged now. At sea there is simply no
// field to stand on, so that branch must win over every other fact, even a (nonsensical) claim of also being
// in a settlement: guarding the precedence stops a future bug elsewhere from smuggling a duel onto open water.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelVenuePolicyTests
   {
      // A settlement stages the duel at the town gates: the ambush settlement-scene path the host already has.
      [Test]
      public void GIVEN_the_player_is_in_a_settlement_and_not_at_sea_WHEN_choosing_THEN_the_venue_is_town_gates()
      {
         DuelVenuePolicy.Choose(false, true).Should().Be(DuelVenue.TownGates);
      }

      // The open land stages it as a wilderness field: the fallback when neither at sea nor in a settlement.
      [Test]
      public void GIVEN_the_player_is_on_the_open_land_WHEN_choosing_THEN_the_venue_is_wilderness()
      {
         DuelVenuePolicy.Choose(false, false).Should().Be(DuelVenue.Wilderness);
      }

      // At sea there is no field to stand on, so the duel must be deferred until the player is ashore.
      [Test]
      public void GIVEN_the_player_is_at_sea_WHEN_choosing_THEN_the_venue_is_not_now()
      {
         DuelVenuePolicy.Choose(true, false).Should().Be(DuelVenue.NotNow);
      }

      // Sea must win over settlement even in the nonsensical case where both are somehow true: this pins the
      // precedence guard directly, since a real player can never truly be both, but the code must not rely on
      // that being enforced elsewhere.
      [Test]
      public void GIVEN_the_player_is_somehow_at_sea_and_in_a_settlement_WHEN_choosing_THEN_sea_wins_and_the_venue_is_not_now()
      {
         DuelVenuePolicy.Choose(true, true).Should().Be(DuelVenue.NotNow);
      }
   }
}
