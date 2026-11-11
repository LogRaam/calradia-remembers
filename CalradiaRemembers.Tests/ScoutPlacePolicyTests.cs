// Code written by Gabriel Mailhot, 10/11/2026.
// What ScoutPlacePolicy decides: whether a character may send the player to scout a place, and what the giver hears
// when the player has it in sight. bdionysis80 (Nexus, 10/11/2026): a council pledged to scout Hvalvik's garrison and no
// task could hold it; Gabriel (10/11/2026): "on fait le type de quete eclairer un lieu". If it is wrong, a lord sends the
// player to scout his own walls, or the report the giver reads is not what the game shows, so the model fills it in.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ScoutPlacePolicyTests
   {
      // STAKE: the report: a place of another realm, even one not at war (Hvalvik was a Nord port at peace), may be scouted.
      [Test]
      public void GIVEN_a_place_of_another_realm_WHEN_a_scouting_is_asked_THEN_it_may_be_set()
      {
         ScoutPlacePolicy.MayAsk(new ScoutPlaceFacts {IsTownCastleOrVillage = true}).Should().BeTrue();
      }

      // STAKE: no one sends a scout to look at their own walls, and the player sees their own realm's places already.
      [Test]
      public void GIVEN_a_place_of_the_givers_or_the_players_realm_WHEN_a_scouting_is_asked_THEN_it_is_not_set()
      {
         ScoutPlacePolicy.MayAsk(new ScoutPlaceFacts {IsTownCastleOrVillage = true, HeldByGiversRealm = true}).Should().BeFalse();
         ScoutPlacePolicy.MayAsk(new ScoutPlaceFacts {IsTownCastleOrVillage = true, HeldByPlayersRealm = true}).Should().BeFalse();
      }

      // STAKE: a name that is no town, castle or village (a misspelling, a hideout, a region) sets no task that can never end.
      [Test]
      public void GIVEN_no_such_place_WHEN_a_scouting_is_asked_THEN_it_is_not_set()
      {
         ScoutPlacePolicy.MayAsk(new ScoutPlaceFacts()).Should().BeFalse();
      }

      // STAKE: the giver reads what the game showed, counted as a scout counts, so the model has no gap to invent into.
      [Test]
      public void GIVEN_a_castle_seen_WHEN_reported_THEN_the_holder_garrison_militia_walls_and_food_are_told()
      {
         ScoutPlacePolicy.Report(new ScoutPlaceSighting {
            Place = "Hvalvik", Kind = "castle", OwnerClan = "Gundaroving", Realm = "Nords", Garrison = 137, Militia = 58, WallLevel = 2, FoodDays = 18, Day = 812
         }).Should().Be("Scouted the castle of Hvalvik on day 812: held by the Gundaroving of the Nords; a garrison of about 140, militia about 60, walls of level 2, food for about 18 days");
      }

      // STAKE: a village has no garrison or walls; its report says what a village has, never a garrison of zero.
      [Test]
      public void GIVEN_a_village_seen_WHEN_reported_THEN_it_tells_militia_and_hearths_and_no_garrison()
      {
         string report = ScoutPlacePolicy.Report(new ScoutPlaceSighting {Place = "Ab Comer", Kind = "village", Realm = "Vlandia", Militia = 24, Hearths = 412, Day = 30});

         report.Should().Contain("militia about 20").And.Contain("about 410 hearths").And.NotContain("garrison").And.NotContain("walls");
      }
   }
}
