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

      // THE CRASH (player report, 17/09/2026): "the game crashes when I try to initiate a duel using the
      // action button… it crashes while loading the battle field", in a settlement named Vaen-Griefendok,
      // which is nobody's Calradia — a modded or total-conversion town.
      //
      // The second argument used to be "is the player in a settlement AT ALL", which is a different question
      // from the one that matters. A settlement whose location complex carries no "center" has no town centre
      // to fight in; DuelEligibilityResolver knew that and withheld the verb correctly, and THIS chose a town
      // venue anyway for a duel armed by the action button. The location came back null and went into the
      // engine unchecked.
      //
      // If this fails, somebody's game is about to close itself when they draw a sword.
      [Test]
      public void GIVEN_a_settlement_with_nowhere_to_fight_WHEN_choosing_THEN_the_duel_goes_to_the_open_field()
      {
         DuelVenuePolicy.Choose(false, false).Should().Be(DuelVenue.Wilderness);
      }

      // The WILDERNESS rather than a refusal, deliberately: a player standing in a town with no duelling
      // ground can still walk out and fight, and a duel they asked for should happen somewhere rather than
      // nowhere. Refusing would trade a crash for the older reported bug — "hit yes, nothing happened".
      [Test]
      public void GIVEN_a_settlement_that_cannot_host_one_WHEN_choosing_THEN_the_duel_is_not_silently_dropped()
      {
         DuelVenuePolicy.Choose(false, false).Should().NotBe(DuelVenue.NotNow);
      }
   }
}
