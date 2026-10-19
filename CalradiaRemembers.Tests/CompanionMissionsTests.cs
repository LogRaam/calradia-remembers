// Code written by Gabriel Mailhot, 22/06/2026.
// Protects the companion-mission decision logic — the governing skill / risk per errand, the attempt
// floor, and the deterministic success bands (incl. capture on a botched risky errand, the loop-closer
// into prisoner rescue). Pure foundation, ahead of the engine glue.
//
// WHY IT MATTERS: CompanionMissionBehavior.Dispatch reads CanAttempt/GoverningSkill to accept or refuse
// an errand before any roll, then Resolve's MissionResult drives ApplyMissionHappiness (a succeeded
// errand lifts the companion, a failed one sours them) and sizes what actually comes home (news items,
// stolen gold). CompanionCaptured is the sole trigger into the risky-errand capture/rescue loop. A wrong
// band here means a companion is refused work they were fit for, paid out loot they never earned, or
// worse, silently NOT captured on a botched steal when the design says they should be.

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionMissionsTests
   {
      // ── Governing skill ────────────────────────────────────────────────────

      // Dispatch reads this both to gate CanAttempt (against the RIGHT vanilla skill) and to word its
      // refusal message ("too unschooled in X"). A wrong mapping checks (and blames) the wrong skill
      // entirely, e.g. refusing a Steal errand for weak Charm instead of weak Roguery.
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

      // ── Payload kind ───────────────────────────────────────────────────────

      // PerformErrand switches on this to pick which gather routine actually runs (news/intel/gold/mood).
      // A wrong mapping runs the wrong routine outright, e.g. an Envoy errand quietly returning stolen
      // gold instead of a read on a faction's mood.
      [Test]
      public void GIVEN_each_errand_WHEN_asked_for_its_payload_THEN_it_maps_to_the_expected_kind()
      {
         //Arrange/Act/Assert
         CompanionMissions.PayloadOf(CompanionMissionType.GatherNews).Should().Be(CompanionMissionPayload.News);
         CompanionMissions.PayloadOf(CompanionMissionType.Spy).Should().Be(CompanionMissionPayload.Intel);
         CompanionMissions.PayloadOf(CompanionMissionType.Steal).Should().Be(CompanionMissionPayload.Gold);
         CompanionMissions.PayloadOf(CompanionMissionType.Barter).Should().Be(CompanionMissionPayload.Gold);
         CompanionMissions.PayloadOf(CompanionMissionType.Envoy).Should().Be(CompanionMissionPayload.Mood);
      }

      // ── Risk ───────────────────────────────────────────────────────────────

      // IsRisky is the ONLY gate on whether a botched errand can capture the companion (Resolve checks it
      // before setting CompanionCaptured), which is the ROADMAP 3.2c loop-closer into prisoner rescue.
      // Missing Spy or Steal here would make that kind of errand permanently safe to send.
      [Test]
      public void GIVEN_a_spy_or_steal_errand_WHEN_asked_THEN_it_is_risky()
      {
         //Arrange/Act/Assert
         CompanionMissions.IsRisky(CompanionMissionType.Spy).Should().BeTrue();
         CompanionMissions.IsRisky(CompanionMissionType.Steal).Should().BeTrue();
      }

      // The complement of the above: a safe errand must never be flagged risky, or a routine "gather news"
      // trip could capture the companion on a bad roll, an outcome the design reserves for Spy/Steal only.
      [Test]
      public void GIVEN_a_news_barter_or_envoy_errand_WHEN_asked_THEN_it_is_not_risky()
      {
         //Arrange/Act/Assert
         CompanionMissions.IsRisky(CompanionMissionType.GatherNews).Should().BeFalse();
         CompanionMissions.IsRisky(CompanionMissionType.Barter).Should().BeFalse();
         CompanionMissions.IsRisky(CompanionMissionType.Envoy).Should().BeFalse();
      }

      // ── Attempt floor ──────────────────────────────────────────────────────

      // Dispatch.CanAttempt gates the errand BEFORE any roll or destination is resolved, so a wrong floor
      // either sends a too-green companion out to fail for certain, or turns away one who was fit for the
      // work. The boundary must be INCLUSIVE (the floor itself allowed) or a companion sitting exactly at
      // MinSkillToAttempt would be wrongly refused.
      [Test]
      public void GIVEN_a_green_companion_WHEN_below_the_skill_floor_THEN_the_errand_is_refused()
      {
         //Arrange/Act/Assert
         CompanionMissions.CanAttempt(24).Should().BeFalse();
         CompanionMissions.CanAttempt(25).Should().BeTrue("the floor itself is allowed");
      }

      // ── Success bands ──────────────────────────────────────────────────────

      // Pins SuccessThreshold. The MissionResult it produces feeds ApplyMissionHappiness (a success lifts
      // the companion via HappinessDriver.ErrandSucceeded) and sizes the full payout (NewsItemsForResult /
      // LootForResult), so a threshold set too high denies a companion a success, and payout, they earned.
      [Test]
      public void GIVEN_high_skill_a_good_roll_and_low_difficulty_WHEN_resolved_THEN_it_fully_succeeds()
      {
         //Arrange — skill 300/3=100, roll 80, diff 10 -> score 170.
         //Act
         var outcome = CompanionMissions.Resolve(new CompanionMissions.ErrandFacts {Type = CompanionMissionType.Barter, GoverningSkillLevel = 300, Roll = 80, Difficulty = 10});

         //Assert
         outcome.Result.Should().Be(MissionResult.Success);
         outcome.CompanionCaptured.Should().BeFalse();
      }

      // Pins PartialThreshold, the floor of the middle band, so a score just shy of full success still
      // pays out the thinner Partial briefing/loot instead of nothing.
      [Test]
      public void GIVEN_a_middling_total_WHEN_resolved_THEN_it_partly_succeeds()
      {
         //Arrange — skill 150/3=50, roll 30, diff 20 -> score 60 (>=50, <100).
         //Act
         var outcome = CompanionMissions.Resolve(new CompanionMissions.ErrandFacts {Type = CompanionMissionType.GatherNews, GoverningSkillLevel = 150, Roll = 30, Difficulty = 20});

         //Assert
         outcome.Result.Should().Be(MissionResult.Partial);
         outcome.CompanionCaptured.Should().BeFalse();
      }

      // A safe errand (Envoy here) must fail cleanly no matter how badly it bottoms out: IsRisky is the
      // only thing allowed to gate capture, so this guards against a regression where the capture check
      // reads only the score and forgets to also check the errand's risk.
      [Test]
      public void GIVEN_a_weak_total_on_a_safe_errand_WHEN_resolved_THEN_it_fails_without_capture()
      {
         //Arrange — skill 60/3=20, roll 10, diff 40 -> score -10 (a safe errand never captures).
         //Act
         var outcome = CompanionMissions.Resolve(new CompanionMissions.ErrandFacts {Type = CompanionMissionType.Envoy, GoverningSkillLevel = 60, Roll = 10, Difficulty = 40});

         //Assert
         outcome.Result.Should().Be(MissionResult.Failure);
         outcome.CompanionCaptured.Should().BeFalse();
      }

      // ── Capture (the loop-closer into prisoner rescue) ─────────────────────

      // THE loop-closer test: CompanionCaptured=true is what OnHeroPrisonerTaken hooks into to flip the
      // errand to AwaitingReunion and apply HappinessDriver.Captured, the ROADMAP 3.2 hand-off from a
      // sent-out companion into the prisoner-rescue arc. Get CaptureThreshold wrong and a risky errand
      // that should end in capture instead reports a bland "failed" with no consequence.
      [Test]
      public void GIVEN_a_botched_risky_errand_WHEN_the_score_bottoms_out_THEN_the_companion_is_captured()
      {
         //Arrange — skill 60/3=20, roll 0, diff 40 -> score -20 (<=0) on a risky steal.
         //Act
         var outcome = CompanionMissions.Resolve(new CompanionMissions.ErrandFacts {Type = CompanionMissionType.Steal, GoverningSkillLevel = 60, Roll = 0, Difficulty = 40});

         //Assert
         outcome.Result.Should().Be(MissionResult.Failure);
         outcome.CompanionCaptured.Should().BeTrue();
      }

      // The far side of the capture boundary: a near miss on a risky errand must still bring the companion
      // home. Without this, an over-eager capture check would turn every failed Spy/Steal errand into a
      // captivity subplot instead of reserving it for a genuinely bottomed-out score.
      [Test]
      public void GIVEN_a_risky_errand_that_merely_falls_short_WHEN_above_the_capture_line_THEN_it_fails_but_the_agent_returns()
      {
         //Arrange — score 10 (>0): a clean failure on a risky errand, no capture.
         //Act — skill 60/3=20, roll 30, diff 40 -> score 10.
         var outcome = CompanionMissions.Resolve(new CompanionMissions.ErrandFacts {Type = CompanionMissionType.Spy, GoverningSkillLevel = 60, Roll = 30, Difficulty = 40});

         //Assert
         outcome.Result.Should().Be(MissionResult.Failure);
         outcome.CompanionCaptured.Should().BeFalse("a near miss is not a capture");
      }

      // ── Defensive clamping ─────────────────────────────────────────────────

      // Resolve is fed a live skill level and a fresh RNG roll from the host; an unvalidated negative
      // skill or an out-of-range roll must not throw mid-Dispatch (which would abort sending the companion
      // out entirely) or silently swing the score outside its designed 0..~130ish range.
      [Test]
      public void GIVEN_out_of_range_inputs_WHEN_resolved_THEN_they_are_clamped_not_thrown()
      {
         //Arrange/Act — negative skill, an over-range roll, a negative difficulty.
         var outcome = CompanionMissions.Resolve(new CompanionMissions.ErrandFacts {Type = CompanionMissionType.Barter, GoverningSkillLevel = -50, Roll = 999, Difficulty = -10});

         //Assert — clamps to skill 0, roll 99, diff 0 -> score 99 -> Partial.
         outcome.Result.Should().Be(MissionResult.Partial);
      }

      // ── Duration ───────────────────────────────────────────────────────────

      // Dispatch sets LingerUntilDay = Today + DurationDays, which the journal's Lingering/Returning
      // status lines and OnDailyTick's advance logic both key off. A wrong base pin desyncs "has reached
      // town" from when the companion's party actually gets there.
      [Test]
      public void GIVEN_a_quick_errand_at_no_difficulty_WHEN_timed_THEN_it_takes_the_base_days()
      {
         //Arrange/Act/Assert — news is a quick errand.
         CompanionMissions.DurationDays(CompanionMissionType.GatherNews, difficulty: 0).Should().Be(2);
      }

      // Pins which errands count as "farther" (Spy/Steal/Envoy) vs "quick" (GatherNews/Barter). Swapping
      // one into the wrong group would make an errand return a day early or late relative to its own
      // journal narration of the trip.
      [Test]
      public void GIVEN_a_farther_errand_WHEN_timed_THEN_it_runs_a_day_longer_than_a_quick_one()
      {
         //Arrange/Act/Assert — spy/steal/envoy carry a base day more than news/barter.
         CompanionMissions.DurationDays(CompanionMissionType.Spy, difficulty: 0).Should().Be(3);
      }

      // Pins the difficulty-to-duration step. Difficulty is currently a single constant per Dispatch
      // (NewsDifficulty), but the scaling itself is load-bearing: a wrong step would make a tougher errand
      // return sooner than an easier one, an inversion nothing else in this file would catch.
      [Test]
      public void GIVEN_rising_difficulty_WHEN_timed_THEN_each_band_adds_a_day_on_the_road()
      {
         //Arrange/Act/Assert — base 2 + one day per 25 difficulty.
         CompanionMissions.DurationDays(CompanionMissionType.GatherNews, difficulty: 50).Should().Be(4);
         CompanionMissions.DurationDays(CompanionMissionType.GatherNews, difficulty: 100).Should().Be(6);
      }

      // ── News report sizing ─────────────────────────────────────────────────

      // GatherIntel/GatherMood/GatherNews all pass their built lines through this before truncating with
      // .Take(count). A cap set too high on a success would try to report more than the destination
      // actually offers; too low shortchanges a companion who earned the full briefing.
      [Test]
      public void GIVEN_a_full_success_WHEN_sizing_the_report_THEN_it_carries_a_full_briefing()
      {
         //Arrange/Act/Assert — capped at the full count even when more are available.
         CompanionMissions.NewsItemsForResult(MissionResult.Success, available: 10).Should().Be(4);
      }

      // Pins that a Partial result gets a visibly thinner report than a Success, so the player can tell
      // the errand only half-worked from the report's substance, not just its own hidden Quality field.
      [Test]
      public void GIVEN_a_partial_WHEN_sizing_the_report_THEN_it_carries_a_thinner_briefing()
      {
         //Arrange/Act/Assert
         CompanionMissions.NewsItemsForResult(MissionResult.Partial, available: 10).Should().Be(2);
      }

      // A failed errand must come back empty-handed, or a "failure" would still hand the player useful
      // intel/news for free, undercutting the whole point of the risk/reward roll.
      [Test]
      public void GIVEN_a_failure_WHEN_sizing_the_report_THEN_it_carries_nothing()
      {
         //Arrange/Act/Assert
         CompanionMissions.NewsItemsForResult(MissionResult.Failure, available: 10).Should().Be(0);
      }

      // A quiet, event-sparse destination must not report more than genuinely happened there, or a
      // success at a dull town would read as inflated / fabricated news rather than an honest thin haul.
      [Test]
      public void GIVEN_fewer_items_available_than_the_cap_WHEN_sizing_THEN_it_never_promises_more_than_exist()
      {
         //Arrange/Act/Assert — a success with only one event to tell.
         CompanionMissions.NewsItemsForResult(MissionResult.Success, available: 1).Should().Be(1);
         CompanionMissions.NewsItemsForResult(MissionResult.Success, available: 0).Should().Be(0);
      }

      // ── Steal loot ─────────────────────────────────────────────────────────

      // m.LootGold flows straight into GiveGoldAction.ApplyBetweenCharacters and gates the "got away with
      // X denars" narration line (only shown when LootGold > 0). A clean success paying out less than the
      // full haul would shortchange the player for a companion's flawless steal.
      [Test]
      public void GIVEN_a_clean_steal_WHEN_scaling_loot_THEN_it_brings_the_full_haul()
      {
         //Arrange/Act/Assert
         CompanionMissions.LootForResult(MissionResult.Success, baseAmount: 500).Should().Be(500);
      }

      // Pins PartialLootPercent (40%). A wrong fraction pays the player either too much for a botched job
      // or too little to feel like the errand was worth the risk of sending a companion out at all.
      [Test]
      public void GIVEN_a_partial_steal_WHEN_scaling_loot_THEN_it_brings_a_fraction()
      {
         //Arrange/Act/Assert — 40% of the base.
         CompanionMissions.LootForResult(MissionResult.Partial, baseAmount: 500).Should().Be(200);
      }

      // A failed steal (often the very roll that gets the companion captured) must yield zero gold, or the
      // player would be rewarded for an errand that, on a risky type, may have cost them a companion.
      [Test]
      public void GIVEN_a_failed_steal_WHEN_scaling_loot_THEN_it_brings_nothing()
      {
         //Arrange/Act/Assert
         CompanionMissions.LootForResult(MissionResult.Failure, baseAmount: 500).Should().Be(0);
      }

      // Defensive floor: LootGold feeds ApplyBetweenCharacters(null, player, LootGold, true) directly, so a
      // negative result here would silently TAKE gold from the player instead of granting nothing on a
      // bad or zero base haul.
      [Test]
      public void GIVEN_a_nonpositive_base_WHEN_scaling_loot_THEN_it_brings_nothing()
      {
         //Arrange/Act/Assert
         CompanionMissions.LootForResult(MissionResult.Success, baseAmount: 0).Should().Be(0);
         CompanionMissions.LootForResult(MissionResult.Success, baseAmount: -100).Should().Be(0);
      }
   }
}
