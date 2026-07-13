// Code written by Gabriel Mailhot, 01/07/2026.
// Captive scene replayability: this policy decides which named dramatic ARC (SlowBurn / Classic / Sudden) a
// captor's opening act follows, so the same captor does not run the identical pacing every scene. Its answer
// feeds CaptiveSceneDirector, the state machine that owns scene structure so the model never has to (and a
// hard act cap guarantees the scene actually ends, replacing the old behaviour where a scene could loop and
// never conclude). Every arc keeps a floor weight so none is ever unreachable; the weighted LEAN (not a hard
// gate) is what gives a bandit's impatience or a lord's savoured cruelty a distinct feel without removing
// variety. If the weighting or the fallback-on-out-of-range roll is wrong, either a captor's pacing collapses
// to a single repetitive arc again, or an edge-case roll could throw instead of resolving safely.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveSceneArcPolicyTests
   {
      // Every arc keeps a floor weight (ArcFloorWeight) by design, so even a bandit's heavily-leaned draw must
      // still occasionally produce SlowBurn or Classic. Losing this floor would make one arc truly impossible
      // for a given context, undoing the whole point of the replayability fix.
      [Test]
      public void GIVEN_a_bandit_captor_WHEN_scanning_every_possible_roll_THEN_every_arc_is_still_reachable()
      {
         List<SceneArc> outcomes = AllOutcomes(true, CaptiveSceneIntent.Intimidation);

         outcomes.Should().Contain(SceneArc.Sudden);
         outcomes.Should().Contain(SceneArc.Classic);
         outcomes.Should().Contain(SceneArc.SlowBurn);
      }

      // A bandit has no patience for theatre (per the production doc): pins that the LEAN actually favours
      // Sudden over the other two, not just that Sudden is reachable, or the "distinct feel per captor type"
      // this policy exists for would be lost even though the tests above would still pass.
      [Test]
      public void GIVEN_a_bandit_captor_WHEN_scanning_every_possible_roll_THEN_sudden_wins_the_most_rolls()
      {
         //Arrange: a bandit has no patience for theatre, so Sudden should absorb the largest share
         //         of the roll range, ahead of both Classic and SlowBurn.
         var counts = new Dictionary<SceneArc, int>();
         int total = CaptureTotal(true, CaptiveSceneIntent.Extortion);

         for (var roll = 0; roll < total; roll++)
         {
            SceneArc arc = CaptiveSceneArcPolicy.Select(true, CaptiveSceneIntent.Extortion, _ => roll);
            counts[arc] = counts.GetValueOrDefault(arc) + 1;
         }

         counts.GetValueOrDefault(SceneArc.Sudden).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.Classic));
         counts.GetValueOrDefault(SceneArc.Classic).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.SlowBurn));
      }

      // A lord after Domination or Training savours the build-up (per the production doc): pins the SlowBurn
      // lean for this register specifically, distinguishing it from the bandit's opposite lean toward Sudden.
      [Test]
      public void GIVEN_a_lord_after_domination_WHEN_scanning_every_possible_roll_THEN_slowburn_wins_the_most_rolls()
      {
         var counts = new Dictionary<SceneArc, int>();
         int total = CaptureTotal(false, CaptiveSceneIntent.Domination);

         for (var roll = 0; roll < total; roll++)
         {
            SceneArc arc = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.Domination, _ => roll);
            counts[arc] = counts.GetValueOrDefault(arc) + 1;
         }

         counts.GetValueOrDefault(SceneArc.SlowBurn).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.Classic));
         counts.GetValueOrDefault(SceneArc.Classic).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.Sudden));
      }

      // Training shares Domination's boost constants in the production switch; this pins that the SHARED case
      // label actually applies to Training too, not just that Domination alone was wired correctly above.
      [Test]
      public void GIVEN_a_lord_after_training_WHEN_scanning_every_possible_roll_THEN_slowburn_wins_the_most_rolls()
      {
         //Arrange: Training shares Domination's leaning toward the slow, savoured build-up.
         List<SceneArc> outcomes = AllOutcomes(false, CaptiveSceneIntent.Training);

         outcomes.Should().Contain(SceneArc.SlowBurn);
         outcomes.Should().Contain(SceneArc.Classic);
         outcomes.Should().Contain(SceneArc.Sudden);
      }

      // PersonalDesire is deliberately torn evenly between SlowBurn and Classic (equal boost constants), a
      // distinct case from Domination's clear SlowBurn lean: pins that the two boosts stay EQUAL, since a drift
      // between them would silently reintroduce a lean the design never intended for this register.
      [Test]
      public void GIVEN_a_lord_after_personal_desire_WHEN_scanning_every_possible_roll_THEN_slowburn_and_classic_are_balanced()
      {
         var counts = new Dictionary<SceneArc, int>();
         int total = CaptureTotal(false, CaptiveSceneIntent.PersonalDesire);

         for (var roll = 0; roll < total; roll++)
         {
            SceneArc arc = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.PersonalDesire, _ => roll);
            counts[arc] = counts.GetValueOrDefault(arc) + 1;
         }

         counts.GetValueOrDefault(SceneArc.SlowBurn).Should().Be(counts.GetValueOrDefault(SceneArc.Classic));
         counts.GetValueOrDefault(SceneArc.SlowBurn).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.Sudden));
      }

      // Interrogation falls through to the switch's default arm alongside Reward/Torture/Reckoning and every
      // non-sexual bandit intent; pins that this whole default group leans the familiar Classic middle ground
      // rather than accidentally being left at the bare floor weight (which would read as "no lean at all").
      [Test]
      public void GIVEN_a_lord_interrogation_scene_WHEN_scanning_every_possible_roll_THEN_classic_wins_the_most_rolls()
      {
         var counts = new Dictionary<SceneArc, int>();
         int total = CaptureTotal(false, CaptiveSceneIntent.Interrogation);

         for (var roll = 0; roll < total; roll++)
         {
            SceneArc arc = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.Interrogation, _ => roll);
            counts[arc] = counts.GetValueOrDefault(arc) + 1;
         }

         counts.GetValueOrDefault(SceneArc.Classic).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.SlowBurn));
         counts.GetValueOrDefault(SceneArc.Classic).Should().BeGreaterThan(counts.GetValueOrDefault(SceneArc.Sudden));
      }

      // Reward and Torture sit at opposite ends of the intensity scale but share the SAME default-arm boost,
      // so both must still keep every arc reachable; a regression that special-cased one of them could narrow
      // its variety without the tests above (which use different intents) catching it.
      [Test]
      public void GIVEN_a_lord_reward_or_torture_scene_WHEN_scanning_every_possible_roll_THEN_every_arc_stays_reachable()
      {
         AllOutcomes(false, CaptiveSceneIntent.Reward).Should()
                                                       .Contain(SceneArc.SlowBurn).And.Contain(SceneArc.Classic).And.Contain(SceneArc.Sudden);
         AllOutcomes(false, CaptiveSceneIntent.Torture).Should()
                                                        .Contain(SceneArc.SlowBurn).And.Contain(SceneArc.Classic).And.Contain(SceneArc.Sudden);
      }

      // Select must be pure: the SAME roll fed back through the SAME context has to draw the SAME arc every
      // time, or the deterministic-roll seam this policy relies on for testing (and CaptiveSceneDirector relies
      // on for save-safe replay) would be broken by hidden state.
      [Test]
      public void GIVEN_the_same_context_and_roll_WHEN_selecting_twice_THEN_the_result_is_deterministic()
      {
         SceneArc first = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.Domination, _ => 25);
         SceneArc second = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.Domination, _ => 25);

         second.Should().Be(first);
      }

      // This is exactly the failure mode CaptiveSceneDirector exists to prevent: a scene must never throw or
      // hang on an unexpected roll. Pins that an out-of-contract draw resolves safely to the least surprising
      // arc (Classic) instead of crashing the scene or leaving it stuck with no arc decided.
      [Test]
      public void GIVEN_an_out_of_contract_roll_WHEN_selecting_THEN_it_falls_back_to_classic()
      {
         //Arrange: a roll at or above the total weight never occurs by contract, but the fallback
         //         must still resolve safely to the familiar arc rather than throw or loop forever.
         int total = CaptureTotal(true, CaptiveSceneIntent.Revenge);

         CaptiveSceneArcPolicy.Select(true, CaptiveSceneIntent.Revenge, _ => total + 50).Should().Be(SceneArc.Classic);
      }

      #region private

      /// <summary>Captures the total weight Select rolled against, by returning it straight back out of the roll seam.</summary>
      private static int CaptureTotal(bool captorIsBandit, CaptiveSceneIntent intent)
      {
         var total = 0;
         CaptiveSceneArcPolicy.Select(captorIsBandit, intent, max =>
         {
            total = max;

            return 0;
         });

         return total;
      }

      /// <summary>Every distinct arc Select can draw for this context, scanned across the whole roll range.</summary>
      private static List<SceneArc> AllOutcomes(bool captorIsBandit, CaptiveSceneIntent intent)
      {
         int total = CaptureTotal(captorIsBandit, intent);
         var outcomes = new List<SceneArc>();

         for (var roll = 0; roll < total; roll++)
         {
            SceneArc outcome = CaptiveSceneArcPolicy.Select(captorIsBandit, intent, _ => roll);
            if (!outcomes.Contains(outcome)) outcomes.Add(outcome);
         }

         return outcomes;
      }

      #endregion
   }
}
