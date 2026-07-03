// Code written by Gabriel Mailhot, 02/07/2026.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CourtActionPolicyTests
   {
      // ── give_influence ────────────────────────────────────────────────────

      [Test]
      public void GIVEN_zero_or_negative_relation_WHEN_computing_the_influence_grant_THEN_it_is_zero()
      {
         CourtActionPolicy.InfluenceGrant(0).Should().Be(0);
         CourtActionPolicy.InfluenceGrant(-10).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_modest_relation_WHEN_computing_the_influence_grant_THEN_it_scales_with_relation()
      {
         //Arrange — base 5 + relation/10.
         CourtActionPolicy.InfluenceGrant(10).Should().Be(6);
         CourtActionPolicy.InfluenceGrant(30).Should().Be(8);
      }

      [Test]
      public void GIVEN_a_very_deep_relation_WHEN_computing_the_influence_grant_THEN_it_is_capped()
      {
         CourtActionPolicy.InfluenceGrant(200).Should().Be(CourtActionPolicy.InfluenceMaxGrant);
      }

      [Test]
      public void GIVEN_a_clan_with_far_more_influence_than_the_grant_WHEN_checking_affordability_THEN_it_can_afford_it()
      {
         CourtActionPolicy.CanAffordInfluenceGrant(100f, 10).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_clan_with_barely_more_influence_than_the_grant_WHEN_checking_affordability_THEN_it_cannot_afford_it()
      {
         //Arrange — the buffer multiplier requires several times the grant in reserve.
         CourtActionPolicy.CanAffordInfluenceGrant(12f, 10).Should().BeFalse();
      }

      [Test]
      public void GIVEN_exactly_the_buffer_multiple_WHEN_checking_affordability_THEN_the_threshold_is_inclusive()
      {
         CourtActionPolicy.CanAffordInfluenceGrant(10 * CourtActionPolicy.InfluenceBufferMultiplier, 10).Should().BeTrue();
      }

      // ── lend_troops ───────────────────────────────────────────────────────

      [Test]
      public void GIVEN_zero_or_negative_relation_WHEN_computing_the_troop_loan_THEN_it_is_zero()
      {
         CourtActionPolicy.TroopLoanCount(0, 50, 20).Should().Be(0);
         CourtActionPolicy.TroopLoanCount(-5, 50, 20).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_generous_relation_and_ample_room_WHEN_computing_the_troop_loan_THEN_it_scales_with_relation()
      {
         //Arrange — relation 25 / 5 = 5 desired, well within the party's spare and the player's room.
         CourtActionPolicy.TroopLoanCount(25, 200, 50).Should().Be(5);
      }

      [Test]
      public void GIVEN_an_extremely_deep_relation_WHEN_computing_the_troop_loan_THEN_it_is_capped()
      {
         CourtActionPolicy.TroopLoanCount(1000, 1000, 1000).Should().Be(CourtActionPolicy.TroopLoanMaxCount);
      }

      [Test]
      public void GIVEN_a_small_party_WHEN_computing_the_troop_loan_THEN_the_npc_never_drops_below_60_percent_strength()
      {
         //Arrange — 10 troops, 60% retained = 6, so at most 4 are spare regardless of relation.
         CourtActionPolicy.TroopLoanCount(1000, 10, 50).Should().Be(4);
      }

      [Test]
      public void GIVEN_the_player_party_nearly_full_WHEN_computing_the_troop_loan_THEN_it_never_exceeds_the_players_room()
      {
         CourtActionPolicy.TroopLoanCount(1000, 1000, 2).Should().Be(2);
      }

      [Test]
      public void GIVEN_no_spare_troops_WHEN_computing_the_troop_loan_THEN_it_is_zero()
      {
         //Arrange — the party is already below the 60% retain floor once rounded (5 troops, must keep 3).
         CourtActionPolicy.TroopLoanCount(50, 5, 10).Should().Be(2);
         CourtActionPolicy.TroopLoanCount(50, 1, 10).Should().Be(0);
      }

      // ── sway_opinion ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_swaying_against_a_target_WHEN_computing_the_delta_THEN_it_is_negative()
      {
         CourtActionPolicy.SwayDelta(true).Should().Be(CourtActionPolicy.SwayAgainstDelta).And.BeNegative();
      }

      [Test]
      public void GIVEN_swaying_for_a_target_WHEN_computing_the_delta_THEN_it_is_positive()
      {
         CourtActionPolicy.SwayDelta(false).Should().Be(CourtActionPolicy.SwayForDelta).And.BePositive();
      }

      // ── sway_opinion target selection ────────────────────────────────────

      [Test]
      public void GIVEN_a_pool_with_close_kin_and_a_stranger_WHEN_selecting_a_sway_target_THEN_kin_is_never_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("spouse", relation: -50, isCloseKin: true),
            new("stranger", relation: 10, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("stranger");
      }

      [Test]
      public void GIVEN_only_close_kin_in_the_pool_WHEN_selecting_a_sway_target_THEN_none_is_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("child", relation: -80, isCloseKin: true),
            new("sibling", relation: -20, isCloseKin: true)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().BeNull();
      }

      [Test]
      public void GIVEN_several_non_kin_candidates_WHEN_selecting_a_sway_target_THEN_the_most_negative_relation_wins()
      {
         //Arrange — an existing rift makes the turn believable; a friend or ally would not credibly turn.
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("friend", relation: 40, isCloseKin: false),
            new("rival", relation: -25, isCloseKin: false),
            new("acquaintance", relation: 0, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("rival");
      }

      [Test]
      public void GIVEN_only_positive_relation_non_kin_candidates_WHEN_selecting_a_sway_target_THEN_the_least_friendly_still_wins()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("closeAlly", relation: 60, isCloseKin: false),
            new("mildAcquaintance", relation: 15, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("mildAcquaintance");
      }

      [Test]
      public void GIVEN_an_empty_pool_WHEN_selecting_a_sway_target_THEN_none_is_chosen()
      {
         CourtActionPolicy.SelectSwayTarget(new List<CourtActionPolicy.SwayCandidate>()).Should().BeNull();
      }

      [Test]
      public void GIVEN_the_worst_regarded_candidate_is_a_faction_leader_WHEN_selecting_a_sway_target_THEN_the_ruler_is_skipped()
      {
         // Moving against a ruler is a war-scale act, the leader's own strategic decision, never a personal
         // rivalry a lord settles at another's word, so a faction leader is never a sway or pledge target.
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("empress", relation: -80, isCloseKin: false, isFactionLeader: true),
            new("rival", relation: -25, isCloseKin: false)
         };

         CourtActionPolicy.SelectSwayTarget(candidates).Should().Be("rival");
         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("rival");
      }

      // ── pledge_against target selection ──────────────────────────────────

      [Test]
      public void GIVEN_a_bitterly_disliked_non_kin_lord_WHEN_selecting_a_pledge_target_THEN_they_are_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("rival", relation: -40, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("rival");
      }

      [Test]
      public void GIVEN_close_kin_held_in_the_same_enmity_WHEN_selecting_a_pledge_target_THEN_kin_is_never_chosen()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("estrangedSibling", relation: -60, isCloseKin: true),
            new("rival", relation: -25, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("rival");
      }

      [Test]
      public void GIVEN_no_candidate_meets_the_animosity_threshold_WHEN_selecting_a_pledge_target_THEN_none_is_chosen()
      {
         //Arrange — a mild dislike is not a genuine cause to move against someone.
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("acquaintance", relation: -5, isCloseKin: false),
            new("friend", relation: 20, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().BeNull();
      }

      [Test]
      public void GIVEN_several_eligible_rivals_WHEN_selecting_a_pledge_target_THEN_the_deepest_enmity_wins()
      {
         var candidates = new List<CourtActionPolicy.SwayCandidate> {
            new("oldGrudge", relation: -25, isCloseKin: false),
            new("bitterEnemy", relation: -70, isCloseKin: false)
         };

         CourtActionPolicy.SelectPledgeTarget(candidates).Should().Be("bitterEnemy");
      }

      [Test]
      public void GIVEN_an_empty_pool_WHEN_selecting_a_pledge_target_THEN_none_is_chosen()
      {
         CourtActionPolicy.SelectPledgeTarget(new List<CourtActionPolicy.SwayCandidate>()).Should().BeNull();
      }

      // ── shared cooldown math ─────────────────────────────────────────────

      [Test]
      public void GIVEN_no_prior_recorded_day_WHEN_checking_the_cooldown_THEN_it_has_elapsed()
      {
         CourtActionPolicy.CooldownElapsed(null, 100, 7).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_cooldown_window_not_yet_passed_WHEN_checking_it_THEN_it_has_not_elapsed()
      {
         CourtActionPolicy.CooldownElapsed(100, 103, 7).Should().BeFalse();
      }

      [Test]
      public void GIVEN_exactly_the_cooldown_length_WHEN_checking_it_THEN_the_threshold_is_inclusive()
      {
         CourtActionPolicy.CooldownElapsed(100, 107, 7).Should().BeTrue();
      }

      [Test]
      public void GIVEN_well_past_the_cooldown_window_WHEN_checking_it_THEN_it_has_elapsed()
      {
         CourtActionPolicy.CooldownElapsed(100, 200, 7).Should().BeTrue();
      }
   }
}
