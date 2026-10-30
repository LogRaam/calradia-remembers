// Code written by Gabriel Mailhot, 30/10/2026.
// A strategist turns to the player for help (Gabriel, 30/10/2026, "go" on the second increment of the strategist aims).
// Decides which lords may ask, which deed they ask (their rival grown too strong, else the dominant realm), the words
// the aim is told with, and when the deed no longer stands. If it is wrong, a lord asks a stranger or an enemy for
// favours, asks the player to strike his own realm or house, asks for a deed the game will not verify (gating before
// declaration, 09/09/2026), or the world fills with lords all wanting the same thing of the player.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Strategy;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Strategy
{
   [TestFixture]
   public sealed class StrategistAimPolicyTests
   {
      private static StrategistGiverFacts Lord(string id, int relation = 5)
         => new() {Id = id, IsHouseHead = true, KnowsThePlayer = true, RelationWithPlayer = relation};

      private static StrategistTargetFacts Target(bool stands = true, bool atWar = true, bool playersOwn = false)
         => new() {Stands = stands, AtWarWithGiversRealm = atWar, IsPlayersOwn = playersOwn};

      // A known, not unfriendly house head at peace with the player may turn to them.
      [Test]
      public void GIVEN_a_house_head_who_knows_the_player_and_does_not_dislike_them_WHEN_weighed_THEN_he_may_turn_to_them()
      {
         StrategistAimPolicy.MayTurnToThePlayer(Lord("derthert")).Should().BeTrue();
      }

      // "Nobles do not hand tasks to strangers" (the quest teaching's own rule); an enemy or a lord who dislikes the
      // player asks no favour; one aim and one outstanding task at a time; the player's own house asks nothing of him.
      [Test]
      public void GIVEN_a_stranger_an_enemy_a_cold_lord_or_one_already_busy_WHEN_weighed_THEN_none_turns_to_the_player()
      {
         StrategistAimPolicy.MayTurnToThePlayer(new StrategistGiverFacts {Id = "a", IsHouseHead = true, RelationWithPlayer = 5}).Should().BeFalse();
         StrategistAimPolicy.MayTurnToThePlayer(new StrategistGiverFacts {Id = "b", IsHouseHead = true, KnowsThePlayer = true, AtWarWithPlayer = true}).Should().BeFalse();
         StrategistAimPolicy.MayTurnToThePlayer(Lord("c", relation: -5)).Should().BeFalse();
         StrategistAimPolicy.MayTurnToThePlayer(new StrategistGiverFacts {Id = "d", IsHouseHead = true, KnowsThePlayer = true, HoldsAnAim = true}).Should().BeFalse();
         StrategistAimPolicy.MayTurnToThePlayer(new StrategistGiverFacts {Id = "e", IsHouseHead = true, KnowsThePlayer = true, HasOutstandingTask = true}).Should().BeFalse();
         StrategistAimPolicy.MayTurnToThePlayer(new StrategistGiverFacts {Id = "f", IsHouseHead = true, KnowsThePlayer = true, IsPlayerSide = true}).Should().BeFalse();
      }

      // A younger son does not pursue his house's strategy: its head does.
      [Test]
      public void GIVEN_a_lord_who_does_not_head_his_house_WHEN_weighed_THEN_he_does_not_turn_to_the_player()
      {
         StrategistAimPolicy.MayTurnToThePlayer(new StrategistGiverFacts {Id = "son", KnowsThePlayer = true, RelationWithPlayer = 30}).Should().BeFalse();
      }

      // "Most get nothing": a handful of lords at once, the warmest toward the player first.
      [Test]
      public void GIVEN_more_willing_lords_than_places_WHEN_chosen_THEN_the_warmest_take_the_places()
      {
         var lords = new List<StrategistGiverFacts> {Lord("cool", 1), Lord("warm", 40), Lord("fond", 20), Lord("mild", 10)};

         StrategistAimPolicy.Choose(lords, StrategistAimPolicy.OpenPlaces(1)).Should().Equal("warm", "fond");
         StrategistAimPolicy.Choose(lords, StrategistAimPolicy.OpenPlaces(StrategistAimPolicy.MaxStandingAims)).Should().BeEmpty();
      }

      // His own rival is what a lord cares about most; the dominant realm is the cause he shares with everyone.
      [Test]
      public void GIVEN_both_a_rival_in_an_enemy_realm_and_a_war_with_the_dominant_realm_WHEN_the_deed_is_chosen_THEN_the_rival_comes_first()
      {
         StrategistAimPolicy.ChooseDeed(Target(), Target()).Should().Be(StrategistDeed.StrikeTheRival);
      }

      // A rival of his own realm cannot be struck in any war; the player is never asked to.
      [Test]
      public void GIVEN_the_rival_is_of_his_own_realm_WHEN_the_deed_is_chosen_THEN_the_dominant_realm_is_asked_instead()
      {
         StrategistAimPolicy.ChooseDeed(Target(atWar: false), Target()).Should().Be(StrategistDeed.BreakTheDominant);
      }

      // The player is never asked to strike his own realm or house, and nothing is asked when there is no war to fight.
      [Test]
      public void GIVEN_only_targets_of_the_players_own_or_at_peace_WHEN_the_deed_is_chosen_THEN_nothing_is_asked()
      {
         StrategistAimPolicy.ChooseDeed(Target(playersOwn: true), Target(atWar: false)).Should().BeNull();
      }

      // A deed that can no longer be done is no longer asked: the rival dead or captive, or peace made.
      [Test]
      public void GIVEN_the_target_fallen_or_the_war_over_WHEN_the_deed_is_read_again_THEN_it_no_longer_stands()
      {
         StrategistAimPolicy.DeedStands(Target(stands: false)).Should().BeFalse();
         StrategistAimPolicy.DeedStands(Target(atWar: false)).Should().BeFalse();
      }

      // The lord says what is true (the house's might, the war), and the menu entry carries the real task the game will
      // verify, under the exact name the quest block needs: gating before declaration, and NPCs never lie. The aim's
      // own words promise no task: the Lean prompt teaches no task-giving, so the promise would go unkept there.
      [Test]
      public void GIVEN_a_rival_deed_WHEN_worded_THEN_the_want_states_the_truth_and_only_the_menu_names_the_task()
      {
         var w = new StrategistAimWording {Deed = StrategistDeed.StrikeTheRival, TargetName = "Unqid", RivalHouse = "Ulmerath", RivalRealm = "the Aserai Sultanate"};

         StrategistAimPolicy.Want(w).Should().Contain("Ulmerath").And.Contain("Unqid").And.Contain("at war with your realm");
         StrategistAimPolicy.PlayerPart(w).Should().Be("The player could defeat Unqid in battle for you.");
         StrategistAimPolicy.MenuCue(w).Should().Contain("Unqid");
         StrategistAimPolicy.TypeToken(w.Deed).Should().Be("attack_lord");
         StrategistAimPolicy.TargetField(w.Deed).Should().Be("target_hero");
      }

      // The dominant realm's deed is the war task the game already verifies against a faction.
      [Test]
      public void GIVEN_a_dominant_realm_deed_WHEN_worded_THEN_it_is_the_attack_faction_task()
      {
         var w = new StrategistAimWording {Deed = StrategistDeed.BreakTheDominant, TargetName = "Northern Empire"};

         StrategistAimPolicy.TypeToken(w.Deed).Should().Be("attack_faction");
         StrategistAimPolicy.TargetField(w.Deed).Should().Be("target_faction");
      }
   }
}
