// Code written by Gabriel Mailhot, 30/10/2026.
// A plot born of threat rather than hatred. Gabriel (29/10/2026) named a lord's ambition as whatever strengthens his
// house or realm, "manigancer contre un rival ou une faction qui devient trop puissante", and agreed (30/10/2026) that
// the lords may plot against the player's own house once it dominates. Until now a lord plotted only out of bitter
// enmity, and never against the player. If this rule is wrong, a lord plots against a friend or his own blood, against a
// ruler (the war-decision system's to move against), against a house no stronger than his own, or the player's mighty
// house is never touched however far it outgrows the rest.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Strategy
{
   [TestFixture]
   public sealed class OvershadowingHouseTests
   {
      private static AutonomousSchemePolicy.SchemeTargetCandidate Head(string id, float houseStrength, int relation = 0, bool playerSide = false,
                                                                       bool kin = false, bool sameHouse = false, bool ruler = false, bool head = true)
         => new() {
            Id = id, Relation = relation, IsHouseHead = head, HouseStrength = houseStrength, OwnHouseStrength = 100f, IsPlayerSide = playerSide,
            IsKin = kin, SameHouse = sameHouse, IsFactionLeader = ruler, RivalRealm = true
         };

      // The heart of Gabriel's words: a house that has grown far stronger than the lord's own is a reason to plot, with no
      // old hatred needed.
      [Test]
      public void GIVEN_a_house_twice_as_strong_and_no_friendship_WHEN_weighed_THEN_its_head_may_be_plotted_against()
      {
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("rival", 250f)).Should().BeTrue();
      }

      // A house only a little stronger is a peer, not a threat: every lord would otherwise plot against half the realm.
      [Test]
      public void GIVEN_a_house_only_somewhat_stronger_WHEN_weighed_THEN_no_plot_is_grounded()
      {
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("peer", 150f)).Should().BeFalse();
      }

      // Envy never turns a lord against someone he likes.
      [Test]
      public void GIVEN_the_head_of_the_mighty_house_is_a_friend_WHEN_weighed_THEN_no_plot_is_grounded()
      {
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("friend", 500f, relation: 10)).Should().BeFalse();
      }

      // Kin, one's own house and a ruler stay out of reach, as for every plot: a ruler grown too strong is answered by war.
      [Test]
      public void GIVEN_kin_the_own_house_or_a_ruler_WHEN_weighed_THEN_none_is_plotted_against()
      {
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("x", 500f, kin: true)).Should().BeFalse();
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("x", 500f, sameHouse: true)).Should().BeFalse();
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("x", 500f, ruler: true)).Should().BeFalse();
      }

      // The house is struck through its head; a younger son of a mighty house is not the one whose standing carries it.
      [Test]
      public void GIVEN_a_member_who_does_not_head_the_house_WHEN_weighed_THEN_he_is_not_the_target()
      {
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("son", 500f, head: false)).Should().BeFalse();
      }

      // Gabriel (30/10/2026): the player's house is no exception once it dominates.
      [Test]
      public void GIVEN_the_players_house_has_grown_too_strong_WHEN_weighed_THEN_the_player_may_be_plotted_against()
      {
         AutonomousSchemePolicy.IsOvershadowingTarget(Head("player", 400f, playerSide: true)).Should().BeTrue();
      }

      // A rival is a house a lord contends with, in his own realm or across a front. Without this, every small house head
      // in Calradia converged on the one mightiest house, often a stranger at the far end of the map he had never met.
      [Test]
      public void GIVEN_a_mighty_house_of_a_distant_realm_at_peace_with_his_WHEN_weighed_THEN_no_plot_is_grounded()
      {
         AutonomousSchemePolicy.SchemeTargetCandidate stranger = new() {Id = "far", IsHouseHead = true, HouseStrength = 900f, OwnHouseStrength = 100f, RivalRealm = false};

         AutonomousSchemePolicy.IsOvershadowingTarget(stranger).Should().BeFalse();
      }

      // One plot at a time against a house grown too strong: otherwise the cap of four could be four plots at once against
      // the same house, the player's when it dominates.
      [Test]
      public void GIVEN_a_mighty_house_already_plotted_against_WHEN_weighed_THEN_no_second_plot_is_grounded()
      {
         AutonomousSchemePolicy.SchemeTargetCandidate struck = new() {Id = "x", IsHouseHead = true, HouseStrength = 900f, OwnHouseStrength = 100f, RivalRealm = true, AlreadyPlottedAgainst = true};

         AutonomousSchemePolicy.IsOvershadowingTarget(struck).Should().BeFalse();
      }

      // When several houses overshadow his own, the lord turns on the mightiest: that is the one tilting the balance.
      [Test]
      public void GIVEN_several_overshadowing_houses_WHEN_one_is_chosen_THEN_it_is_the_mightiest()
      {
         var candidates = new List<AutonomousSchemePolicy.SchemeTargetCandidate> {Head("strong", 300f), Head("mightiest", 900f), Head("peer", 120f)};

         AutonomousSchemePolicy.SelectOvershadowingTarget(candidates).Should().Be("mightiest");
      }

      // A lord whose house is on a par with everyone has no threat to answer, and plots nothing.
      [Test]
      public void GIVEN_no_house_overshadows_his_own_WHEN_one_is_chosen_THEN_there_is_none()
      {
         AutonomousSchemePolicy.SelectOvershadowingTarget(new[] {Head("peer", 150f), Head("friend", 900f, relation: 20)}).Should().BeNull();
      }
   }
}
