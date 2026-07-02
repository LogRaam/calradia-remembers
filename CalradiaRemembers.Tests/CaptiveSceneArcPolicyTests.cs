// Code written by Gabriel Mailhot, 01/07/2026.

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
      [Test]
      public void GIVEN_a_bandit_captor_WHEN_scanning_every_possible_roll_THEN_every_arc_is_still_reachable()
      {
         List<SceneArc> outcomes = AllOutcomes(true, CaptiveSceneIntent.Intimidation);

         outcomes.Should().Contain(SceneArc.Sudden);
         outcomes.Should().Contain(SceneArc.Classic);
         outcomes.Should().Contain(SceneArc.SlowBurn);
      }

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

      [Test]
      public void GIVEN_a_lord_after_training_WHEN_scanning_every_possible_roll_THEN_slowburn_wins_the_most_rolls()
      {
         //Arrange: Training shares Domination's leaning toward the slow, savoured build-up.
         List<SceneArc> outcomes = AllOutcomes(false, CaptiveSceneIntent.Training);

         outcomes.Should().Contain(SceneArc.SlowBurn);
         outcomes.Should().Contain(SceneArc.Classic);
         outcomes.Should().Contain(SceneArc.Sudden);
      }

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

      [Test]
      public void GIVEN_a_lord_reward_or_torture_scene_WHEN_scanning_every_possible_roll_THEN_every_arc_stays_reachable()
      {
         AllOutcomes(false, CaptiveSceneIntent.Reward).Should()
                                                       .Contain(SceneArc.SlowBurn).And.Contain(SceneArc.Classic).And.Contain(SceneArc.Sudden);
         AllOutcomes(false, CaptiveSceneIntent.Torture).Should()
                                                        .Contain(SceneArc.SlowBurn).And.Contain(SceneArc.Classic).And.Contain(SceneArc.Sudden);
      }

      [Test]
      public void GIVEN_the_same_context_and_roll_WHEN_selecting_twice_THEN_the_result_is_deterministic()
      {
         SceneArc first = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.Domination, _ => 25);
         SceneArc second = CaptiveSceneArcPolicy.Select(false, CaptiveSceneIntent.Domination, _ => 25);

         second.Should().Be(first);
      }

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
