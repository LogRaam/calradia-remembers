// Code written by Gabriel Mailhot, 01/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveEncounterPolicyTests
   {
      [Test]
      public void GIVEN_a_capped_base_plus_bonuses_WHEN_computing_probability_THEN_the_total_is_capped()
      {
         //Arrange: base capped 0.60 + 0.20 bonus = 0.80, but the total caps at 0.70.
         CaptiveEncounterPolicy.EncounterProbability(10, Kinks(Kink.PowerImbalance, Kink.FreeUse))
                               .Should().BeApproximately(0.70f, 0.0001f);
      }

      // ── Bandit captor rolls (added 01/07/2026) ──────────────────────────────

      [Test]
      public void GIVEN_a_female_prisoner_and_a_low_roll_WHEN_selecting_bandit_intent_THEN_it_is_personal_desire()
      {
         //Arrange: female spoil chance is 50, so a roll of 0 always clears it.
         RomanticProfile romantic = new() {IsFemale = false, Orientation = SexualOrientation.Heterosexual};

         CaptiveEncounterPolicy.SelectBanditIntent(true, romantic, _ => 0).Should().Be(CaptiveSceneIntent.PersonalDesire);
      }

      // ── EncounterProbability ───────────────────────────────────────────────

      [Test]
      public void GIVEN_a_few_days_and_no_kinks_WHEN_computing_probability_THEN_it_climbs_with_the_days()
      {
         CaptiveEncounterPolicy.EncounterProbability(3, Kinks()).Should().BeApproximately(0.45f, 0.0001f);
      }

      [Test]
      public void GIVEN_a_kink_that_carries_no_bonus_WHEN_computing_probability_THEN_only_the_base_applies()
      {
         //Arrange: Praise is not one of the predatory bonus kinks.
         CaptiveEncounterPolicy.EncounterProbability(3, Kinks(Kink.Praise)).Should().BeApproximately(0.45f, 0.0001f);
      }

      [Test]
      public void GIVEN_a_lone_fighter_WHEN_capping_collective_participants_THEN_no_extra_bandit_can_join()
      {
         //Arrange: the chief IS the one fighter that survived, so there is nobody left to add.
         CaptiveEncounterPolicy.BanditCollectiveParticipantCap(1).Should().Be(0);
      }

      [Test]
      public void GIVEN_a_long_captivity_WHEN_computing_probability_THEN_the_base_caps()
      {
         //Arrange: 10 days would be 1.5, but the base is capped at 0.60.
         CaptiveEncounterPolicy.EncounterProbability(10, Kinks()).Should().BeApproximately(0.60f, 0.0001f);
      }

      [Test]
      public void GIVEN_a_male_prisoner_WHEN_selecting_bandit_intent_THEN_the_spoil_chance_is_near_nil()
      {
         //Arrange: male spoil chance is 3, so a roll of 3 already misses it (falls to the menace ladder).
         RomanticProfile romantic = new() {IsFemale = false, Orientation = SexualOrientation.Bisexual};

         CaptiveEncounterPolicy.SelectBanditIntent(false, romantic, _ => 3).Should().NotBe(CaptiveSceneIntent.PersonalDesire);
      }

      [Test]
      public void GIVEN_a_non_desire_scene_WHEN_reading_the_collective_chance_THEN_it_is_25()
      {
         CaptiveEncounterPolicy.BanditCollectiveChance(CaptiveSceneIntent.Extortion).Should().Be(25);
      }

      [Test]
      public void GIVEN_a_personal_desire_scene_WHEN_reading_the_collective_chance_THEN_it_is_55()
      {
         CaptiveEncounterPolicy.BanditCollectiveChance(CaptiveSceneIntent.PersonalDesire).Should().Be(55);
      }

      [Test]
      public void GIVEN_a_praise_kink_and_warm_regard_WHEN_computing_the_total_weight_THEN_it_is_larger_than_without_the_praise_kink()
      {
         //Arrange: the praise kink should sharpen (not merely allow) reward once it is reachable.
         int withoutPraise = CaptureTotal(Kinks(), 20);
         int withPraise = CaptureTotal(Kinks(Kink.Praise), 20);

         withPraise.Should().BeGreaterThan(withoutPraise);
      }

      [Test]
      public void GIVEN_a_sadistic_captor_and_neutral_regard_WHEN_the_roll_lands_at_the_bottom_of_the_range_THEN_it_is_still_interrogation_not_torture()
      {
         //Arrange: Interrogation always keeps a floor weight and is drawn first, so even a sadist's
         //         captive scene is not FORCED into torture: a roll of 0 lands on the floor band.
         CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), 0, _ => 0)
                               .Should().Be(CaptiveSceneIntent.Interrogation);
      }

      [Test]
      public void GIVEN_a_sadistic_captor_and_neutral_regard_WHEN_the_roll_lands_at_the_top_of_the_range_THEN_it_leans_torture()
      {
         //Arrange: capture the total weight first, then draw the very top of the range. With no
         //         Training/Reward weight (kink absent, relation neutral) the heaviest boosted band,
         //         Torture, sits last and absorbs the top roll.
         int total = CaptureTotal(Kinks(Kink.Sadism), 0);

         CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), 0, _ => total - 1)
                               .Should().Be(CaptiveSceneIntent.Torture);
      }

      [Test]
      public void GIVEN_a_two_fighter_band_WHEN_capping_collective_participants_THEN_one_extra_bandit_can_join()
      {
         CaptiveEncounterPolicy.BanditCollectiveParticipantCap(2).Should().Be(1);
      }

      [Test]
      public void GIVEN_an_extreme_kink_and_regard_combination_WHEN_scanning_every_possible_roll_THEN_all_four_floor_intents_stay_reachable()
      {
         //Arrange: a maximally hostile draw (deep contempt, every torture/domination/desire kink
         //         stacked) must still leave Interrogation, PersonalDesire, Domination AND Torture
         //         all reachable: no valid intent is ever impossible for a given captor.
         List<Kink> kinks = Kinks(Kink.Sadism, Kink.ImpactPlay, Kink.FearPlay, Kink.PowerImbalance, Kink.Dominance,
            Kink.MasterSlave, Kink.Objectification, Kink.Degradation, Kink.FreeUse, Kink.Breeding,
            Kink.OrgasmControl, Kink.PetPlay);

         List<CaptiveSceneIntent> outcomes = AllOutcomes(kinks, -15);

         outcomes.Should().Contain(CaptiveSceneIntent.Interrogation);
         outcomes.Should().Contain(CaptiveSceneIntent.PersonalDesire);
         outcomes.Should().Contain(CaptiveSceneIntent.Domination);
         outcomes.Should().Contain(CaptiveSceneIntent.Torture);
      }

      [Test]
      public void GIVEN_an_incompatible_orientation_WHEN_selecting_bandit_intent_THEN_it_falls_to_the_menace_ladder()
      {
         //Arrange: male prisoner, roll clears the 3% spoil threshold, but a heterosexual male captor
         //         is not compatible with a male prisoner, so the branch is skipped.
         RomanticProfile romantic = new() {IsFemale = false, Orientation = SexualOrientation.Heterosexual};

         CaptiveEncounterPolicy.SelectBanditIntent(false, romantic, _ => 0).Should().Be(CaptiveSceneIntent.Extortion);
      }

      [Test]
      public void GIVEN_extra_bandits_and_a_roll_at_or_above_the_chance_WHEN_deciding_whether_the_scene_is_collective_THEN_it_is_not()
      {
         CaptiveEncounterPolicy.ShouldBanditSceneBeCollective(CaptiveSceneIntent.Extortion, 2, _ => 25).Should().BeFalse();
      }

      [Test]
      public void GIVEN_extra_bandits_and_a_roll_under_the_chance_WHEN_deciding_whether_the_scene_is_collective_THEN_it_is()
      {
         CaptiveEncounterPolicy.ShouldBanditSceneBeCollective(CaptiveSceneIntent.PersonalDesire, 2, _ => 0).Should().BeTrue();
      }

      [Test]
      public void GIVEN_mildly_negative_regard_WHEN_scanning_every_possible_roll_THEN_reward_is_never_drawn()
      {
         AllOutcomes(Kinks(), -1).Should().NotContain(CaptiveSceneIntent.Reward);
      }

      [Test]
      public void GIVEN_no_extra_bandits_WHEN_deciding_whether_the_scene_is_collective_THEN_it_never_rolls_and_stays_solo()
      {
         //Arrange: the roll function throws if called, proving the && short-circuits.
         CaptiveEncounterPolicy.ShouldBanditSceneBeCollective(CaptiveSceneIntent.PersonalDesire, 0, _ => throw new Exception("should not roll"))
                               .Should().BeFalse();
      }

      [Test]
      public void GIVEN_no_training_kink_WHEN_scanning_every_possible_roll_THEN_training_is_never_drawn()
      {
         AllOutcomes(Kinks(Kink.Sadism, Kink.Praise), 30).Should().NotContain(CaptiveSceneIntent.Training);
      }

      // ── SelectIntent weighted draw (rewritten 01/07/2026) ───────────────────

      [Test]
      public void GIVEN_null_kinks_WHEN_computing_probability_THEN_only_the_base_applies()
      {
         CaptiveEncounterPolicy.EncounterProbability(2, null).Should().BeApproximately(0.30f, 0.0001f);
      }

      [Test]
      public void GIVEN_one_extra_bandit_WHEN_counting_bandit_witnesses_THEN_only_one_is_shown()
      {
         CaptiveEncounterPolicy.BanditWitnessCount(1).Should().Be(1);
      }

      [Test]
      public void GIVEN_predatory_kinks_WHEN_computing_probability_THEN_their_bonuses_add()
      {
         //Arrange: 3 days (0.45) + PowerImbalance (0.10) + FreeUse (0.10) = 0.65.
         CaptiveEncounterPolicy.EncounterProbability(3, Kinks(Kink.PowerImbalance, Kink.FreeUse))
                               .Should().BeApproximately(0.65f, 0.0001f);
      }

      [Test]
      public void GIVEN_strong_contempt_WHEN_scanning_every_possible_roll_THEN_reward_is_never_drawn()
      {
         AllOutcomes(Kinks(Kink.Praise), -15).Should().NotContain(CaptiveSceneIntent.Reward);
      }

      [Test]
      public void GIVEN_the_menace_ladder_WHEN_the_second_roll_is_75_or_more_THEN_it_is_revenge()
      {
         var calls = 0;


         int Roll(int max)
         {
            calls++;

            return calls == 1
               ? 99
               : 90;
         }


         CaptiveEncounterPolicy.SelectBanditIntent(false, null, Roll).Should().Be(CaptiveSceneIntent.Revenge);
      }

      [Test]
      public void GIVEN_the_menace_ladder_WHEN_the_second_roll_is_between_45_and_74_THEN_it_is_intimidation()
      {
         var calls = 0;


         int Roll(int max)
         {
            calls++;

            return calls == 1
               ? 99 // misses the spoil chance
               : 60; // lands in the intimidation band
         }


         CaptiveEncounterPolicy.SelectBanditIntent(false, null, Roll).Should().Be(CaptiveSceneIntent.Intimidation);
      }

      [Test]
      public void GIVEN_the_menace_ladder_WHEN_the_second_roll_is_under_45_THEN_it_is_extortion()
      {
         var calls = 0;


         int Roll(int max)
         {
            calls++;

            return calls == 1
               ? 99 // misses the spoil chance
               : 44; // lands in the extortion band
         }


         CaptiveEncounterPolicy.SelectBanditIntent(false, null, Roll).Should().Be(CaptiveSceneIntent.Extortion);
      }

      [Test]
      public void GIVEN_the_training_kink_WHEN_scanning_every_possible_roll_THEN_training_is_reachable()
      {
         AllOutcomes(Kinks(Kink.Training), 0).Should().Contain(CaptiveSceneIntent.Training);
      }

      [Test]
      public void GIVEN_three_extra_bandits_WHEN_counting_bandit_witnesses_THEN_it_is_capped_at_two()
      {
         CaptiveEncounterPolicy.BanditWitnessCount(3).Should().Be(2);
      }

      [Test]
      public void GIVEN_warm_regard_WHEN_scanning_every_possible_roll_THEN_reward_becomes_reachable()
      {
         AllOutcomes(Kinks(), 20).Should().Contain(CaptiveSceneIntent.Reward);
      }

      #region private

      /// <summary>Every distinct intent SelectIntent can draw for this kink/relation pair, scanned across the whole roll range.</summary>
      private static List<CaptiveSceneIntent> AllOutcomes(List<Kink> kinks, int relation)
      {
         int total = CaptureTotal(kinks, relation);
         var outcomes = new List<CaptiveSceneIntent>();

         for (var roll = 0; roll < total; roll++)
         {
            CaptiveSceneIntent outcome = CaptiveEncounterPolicy.SelectIntent(kinks, relation, _ => roll);
            if (!outcomes.Contains(outcome)) outcomes.Add(outcome);
         }

         return outcomes;
      }

      /// <summary>Captures the total weight SelectIntent rolled against, by returning it straight back out of the roll seam.</summary>
      private static int CaptureTotal(List<Kink> kinks, int relation)
      {
         var total = 0;
         CaptiveEncounterPolicy.SelectIntent(kinks, relation, max =>
         {
            total = max;

            return 0;
         });

         return total;
      }

      private static List<Kink> Kinks(params Kink[] kinks) => new(kinks);

      #endregion
   }
}