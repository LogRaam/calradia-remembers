// Code written by Gabriel Mailhot, 22/06/2026.
// Protects the companion-mission decision logic — the governing skill / risk per errand, the attempt
// floor, and the deterministic success bands (incl. capture on a botched risky errand, the loop-closer
// into prisoner rescue). Pure foundation, ahead of the engine glue.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionMissionsTests
   {
      // ── Governing skill ────────────────────────────────────────────────────

      [Test]
      public void GIVEN_each_errand_WHEN_asked_for_its_skill_THEN_it_maps_to_the_expected_one()
      {
         //Arrange/Act/Assert
         CompanionMissions.GoverningSkill(CompanionMissionType.GatherNews).Should().Be(MissionSkill.Scouting);
         CompanionMissions.GoverningSkill(CompanionMissionType.Spy).Should().Be(MissionSkill.Roguery);
         CompanionMissions.GoverningSkill(CompanionMissionType.Steal).Should().Be(MissionSkill.Roguery);
         CompanionMissions.GoverningSkill(CompanionMissionType.Barter).Should().Be(MissionSkill.Trade);
         CompanionMissions.GoverningSkill(CompanionMissionType.Envoy).Should().Be(MissionSkill.Charm);
      }

      // ── Risk ───────────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_spy_or_steal_errand_WHEN_asked_THEN_it_is_risky()
      {
         //Arrange/Act/Assert
         CompanionMissions.IsRisky(CompanionMissionType.Spy).Should().BeTrue();
         CompanionMissions.IsRisky(CompanionMissionType.Steal).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_news_barter_or_envoy_errand_WHEN_asked_THEN_it_is_not_risky()
      {
         //Arrange/Act/Assert
         CompanionMissions.IsRisky(CompanionMissionType.GatherNews).Should().BeFalse();
         CompanionMissions.IsRisky(CompanionMissionType.Barter).Should().BeFalse();
         CompanionMissions.IsRisky(CompanionMissionType.Envoy).Should().BeFalse();
      }

      // ── Attempt floor ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_green_companion_WHEN_below_the_skill_floor_THEN_the_errand_is_refused()
      {
         //Arrange/Act/Assert
         CompanionMissions.CanAttempt(24).Should().BeFalse();
         CompanionMissions.CanAttempt(25).Should().BeTrue("the floor itself is allowed");
      }

      // ── Success bands ──────────────────────────────────────────────────────

      [Test]
      public void GIVEN_high_skill_a_good_roll_and_low_difficulty_WHEN_resolved_THEN_it_fully_succeeds()
      {
         //Arrange — skill 300/3=100, roll 80, diff 10 -> score 170.
         //Act
         var outcome = CompanionMissions.Resolve(CompanionMissionType.Barter, governingSkillLevel: 300, roll: 80, difficulty: 10);

         //Assert
         outcome.Result.Should().Be(MissionResult.Success);
         outcome.CompanionCaptured.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_middling_total_WHEN_resolved_THEN_it_partly_succeeds()
      {
         //Arrange — skill 150/3=50, roll 30, diff 20 -> score 60 (>=50, <100).
         //Act
         var outcome = CompanionMissions.Resolve(CompanionMissionType.GatherNews, governingSkillLevel: 150, roll: 30, difficulty: 20);

         //Assert
         outcome.Result.Should().Be(MissionResult.Partial);
         outcome.CompanionCaptured.Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_weak_total_on_a_safe_errand_WHEN_resolved_THEN_it_fails_without_capture()
      {
         //Arrange — skill 60/3=20, roll 10, diff 40 -> score -10 (a safe errand never captures).
         //Act
         var outcome = CompanionMissions.Resolve(CompanionMissionType.Envoy, governingSkillLevel: 60, roll: 10, difficulty: 40);

         //Assert
         outcome.Result.Should().Be(MissionResult.Failure);
         outcome.CompanionCaptured.Should().BeFalse();
      }

      // ── Capture (the loop-closer into prisoner rescue) ─────────────────────

      [Test]
      public void GIVEN_a_botched_risky_errand_WHEN_the_score_bottoms_out_THEN_the_companion_is_captured()
      {
         //Arrange — skill 60/3=20, roll 0, diff 40 -> score -20 (<=0) on a risky steal.
         //Act
         var outcome = CompanionMissions.Resolve(CompanionMissionType.Steal, governingSkillLevel: 60, roll: 0, difficulty: 40);

         //Assert
         outcome.Result.Should().Be(MissionResult.Failure);
         outcome.CompanionCaptured.Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_risky_errand_that_merely_falls_short_WHEN_above_the_capture_line_THEN_it_fails_but_the_agent_returns()
      {
         //Arrange — score 10 (>0): a clean failure on a risky errand, no capture.
         //Act — skill 60/3=20, roll 30, diff 40 -> score 10.
         var outcome = CompanionMissions.Resolve(CompanionMissionType.Spy, governingSkillLevel: 60, roll: 30, difficulty: 40);

         //Assert
         outcome.Result.Should().Be(MissionResult.Failure);
         outcome.CompanionCaptured.Should().BeFalse("a near miss is not a capture");
      }

      // ── Defensive clamping ─────────────────────────────────────────────────

      [Test]
      public void GIVEN_out_of_range_inputs_WHEN_resolved_THEN_they_are_clamped_not_thrown()
      {
         //Arrange/Act — negative skill, an over-range roll, a negative difficulty.
         var outcome = CompanionMissions.Resolve(CompanionMissionType.Barter, governingSkillLevel: -50, roll: 999, difficulty: -10);

         //Assert — clamps to skill 0, roll 99, diff 0 -> score 99 -> Partial.
         outcome.Result.Should().Be(MissionResult.Partial);
      }

      // ── Duration ───────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_quick_errand_at_no_difficulty_WHEN_timed_THEN_it_takes_the_base_days()
      {
         //Arrange/Act/Assert — news is a quick errand.
         CompanionMissions.DurationDays(CompanionMissionType.GatherNews, difficulty: 0).Should().Be(2);
      }

      [Test]
      public void GIVEN_a_farther_errand_WHEN_timed_THEN_it_runs_a_day_longer_than_a_quick_one()
      {
         //Arrange/Act/Assert — spy/steal/envoy carry a base day more than news/barter.
         CompanionMissions.DurationDays(CompanionMissionType.Spy, difficulty: 0).Should().Be(3);
      }

      [Test]
      public void GIVEN_rising_difficulty_WHEN_timed_THEN_each_band_adds_a_day_on_the_road()
      {
         //Arrange/Act/Assert — base 2 + one day per 25 difficulty.
         CompanionMissions.DurationDays(CompanionMissionType.GatherNews, difficulty: 50).Should().Be(4);
         CompanionMissions.DurationDays(CompanionMissionType.GatherNews, difficulty: 100).Should().Be(6);
      }

      // ── News report sizing ─────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_full_success_WHEN_sizing_the_report_THEN_it_carries_a_full_briefing()
      {
         //Arrange/Act/Assert — capped at the full count even when more are available.
         CompanionMissions.NewsItemsForResult(MissionResult.Success, available: 10).Should().Be(4);
      }

      [Test]
      public void GIVEN_a_partial_WHEN_sizing_the_report_THEN_it_carries_a_thinner_briefing()
      {
         //Arrange/Act/Assert
         CompanionMissions.NewsItemsForResult(MissionResult.Partial, available: 10).Should().Be(2);
      }

      [Test]
      public void GIVEN_a_failure_WHEN_sizing_the_report_THEN_it_carries_nothing()
      {
         //Arrange/Act/Assert
         CompanionMissions.NewsItemsForResult(MissionResult.Failure, available: 10).Should().Be(0);
      }

      [Test]
      public void GIVEN_fewer_items_available_than_the_cap_WHEN_sizing_THEN_it_never_promises_more_than_exist()
      {
         //Arrange/Act/Assert — a success with only one event to tell.
         CompanionMissions.NewsItemsForResult(MissionResult.Success, available: 1).Should().Be(1);
         CompanionMissions.NewsItemsForResult(MissionResult.Success, available: 0).Should().Be(0);
      }

      // ── Steal loot ─────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_clean_steal_WHEN_scaling_loot_THEN_it_brings_the_full_haul()
      {
         //Arrange/Act/Assert
         CompanionMissions.LootForResult(MissionResult.Success, baseAmount: 500).Should().Be(500);
      }

      [Test]
      public void GIVEN_a_partial_steal_WHEN_scaling_loot_THEN_it_brings_a_fraction()
      {
         //Arrange/Act/Assert — 40% of the base.
         CompanionMissions.LootForResult(MissionResult.Partial, baseAmount: 500).Should().Be(200);
      }

      [Test]
      public void GIVEN_a_failed_steal_WHEN_scaling_loot_THEN_it_brings_nothing()
      {
         //Arrange/Act/Assert
         CompanionMissions.LootForResult(MissionResult.Failure, baseAmount: 500).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_nonpositive_base_WHEN_scaling_loot_THEN_it_brings_nothing()
      {
         //Arrange/Act/Assert
         CompanionMissions.LootForResult(MissionResult.Success, baseAmount: 0).Should().Be(0);
         CompanionMissions.LootForResult(MissionResult.Success, baseAmount: -100).Should().Be(0);
      }
   }
}
