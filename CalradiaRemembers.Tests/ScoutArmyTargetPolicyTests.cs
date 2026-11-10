// Code written by Gabriel Mailhot, 10/11/2026.
// What ScoutArmyTargetPolicy decides: whose army a scout_army task sends the player to observe. If it is wrong, a
// council that pledged to scout one place sets the player on another realm's army (bdionysis80, Nexus 10/11/2026:
// Hvalvik, a Nord port, became "Scout an army of Sturgia"), or a task names a realm the player is not even at war with.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ScoutArmyTargetPolicyTests
   {
      // STAKE (bdionysis80): the council named Hvalvik, held by a realm the player is not at war with; no task is set,
      // rather than an army of Sturgia in its stead.
      [Test]
      public void GIVEN_a_named_place_held_by_a_realm_not_at_war_WHEN_the_target_is_decided_THEN_no_task_is_set()
      {
         ScoutArmyTargetPolicy.Decide(new ScoutArmyTargetFacts {NamesPlace = true, PlaceFound = true, PlaceOwnerAtWarWithGiver = false})
                              .Should().Be(ScoutArmyTarget.Refuse);
      }

      // STAKE: a place held by an enemy sends the player to that enemy's armies, the realm the table spoke of.
      [Test]
      public void GIVEN_a_named_place_held_by_an_enemy_WHEN_the_target_is_decided_THEN_it_is_an_army_of_that_enemy()
      {
         ScoutArmyTargetPolicy.Decide(new ScoutArmyTargetFacts {NamesPlace = true, PlaceFound = true, PlaceOwnerAtWarWithGiver = true})
                              .Should().Be(ScoutArmyTarget.PlaceOwner);
      }

      // STAKE: a place the game cannot find is not quietly replaced by the first enemy realm.
      [Test]
      public void GIVEN_a_named_place_the_world_does_not_hold_WHEN_the_target_is_decided_THEN_no_task_is_set()
      {
         ScoutArmyTargetPolicy.Decide(new ScoutArmyTargetFacts {NamesPlace = true, PlaceFound = false})
                              .Should().Be(ScoutArmyTarget.Refuse);
      }

      // STAKE: an army's leader or a realm named outright is what the giver asked for, whatever place is mentioned too.
      [Test]
      public void GIVEN_a_named_leader_or_realm_WHEN_the_target_is_decided_THEN_it_wins_over_the_place()
      {
         ScoutArmyTargetPolicy.Decide(new ScoutArmyTargetFacts {NamesHero = true, NamesFaction = true, NamesPlace = true})
                              .Should().Be(ScoutArmyTarget.NamedHero);
         ScoutArmyTargetPolicy.Decide(new ScoutArmyTargetFacts {NamesFaction = true, NamesPlace = true, PlaceFound = true})
                              .Should().Be(ScoutArmyTarget.NamedFaction);
      }

      // STAKE: a task that names no target at all still finds an enemy army to watch, as before.
      [Test]
      public void GIVEN_nothing_named_WHEN_the_target_is_decided_THEN_it_is_the_first_enemy_realm()
      {
         ScoutArmyTargetPolicy.Decide(new ScoutArmyTargetFacts()).Should().Be(ScoutArmyTarget.FirstEnemy);
      }
   }
}
