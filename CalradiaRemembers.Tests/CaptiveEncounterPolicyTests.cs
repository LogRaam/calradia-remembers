// Code written by Gabriel Mailhot, 20/06/2026.
// Protects captive-encounter pacing (how often a captor acts) and the intent ladder (which scene
// they choose). Both break in silence — a probability drift makes the feature spam or never fire,
// and the intent ladder's order + the &&/|| precedence on the PersonalDesire rung are easy to break.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveEncounterPolicyTests
    {
        // ── EncounterProbability ───────────────────────────────────────────────

        [Test]
        public void GIVEN_a_few_days_and_no_kinks_WHEN_computing_probability_THEN_it_climbs_with_the_days()
        {
            CaptiveEncounterPolicy.EncounterProbability(3, Kinks()).Should().BeApproximately(0.30f, 0.0001f);
        }

        [Test]
        public void GIVEN_a_long_captivity_WHEN_computing_probability_THEN_the_base_caps()
        {
            //Arrange — 10 days would be 1.0, but the base is capped at 0.50.
            CaptiveEncounterPolicy.EncounterProbability(10, Kinks()).Should().BeApproximately(0.50f, 0.0001f);
        }

        [Test]
        public void GIVEN_predatory_kinks_WHEN_computing_probability_THEN_their_bonuses_add()
        {
            //Arrange — 3 days (0.30) + PowerImbalance (0.10) + FreeUse (0.10) = 0.50.
            CaptiveEncounterPolicy.EncounterProbability(3, Kinks(Kink.PowerImbalance, Kink.FreeUse))
                .Should().BeApproximately(0.50f, 0.0001f);
        }

        [Test]
        public void GIVEN_a_capped_base_plus_bonuses_WHEN_computing_probability_THEN_the_total_is_capped()
        {
            //Arrange — base 0.50 + 0.20 bonus = 0.70, but the total caps at 0.60.
            CaptiveEncounterPolicy.EncounterProbability(10, Kinks(Kink.PowerImbalance, Kink.FreeUse))
                .Should().BeApproximately(0.60f, 0.0001f);
        }

        [Test]
        public void GIVEN_a_kink_that_carries_no_bonus_WHEN_computing_probability_THEN_only_the_base_applies()
        {
            //Arrange — Praise is not one of the predatory bonus kinks.
            CaptiveEncounterPolicy.EncounterProbability(3, Kinks(Kink.Praise)).Should().BeApproximately(0.30f, 0.0001f);
        }

        [Test]
        public void GIVEN_null_kinks_WHEN_computing_probability_THEN_only_the_base_applies()
        {
            CaptiveEncounterPolicy.EncounterProbability(2, null).Should().BeApproximately(0.20f, 0.0001f);
        }

        // ── SelectIntent ladder ────────────────────────────────────────────────

        [Test]
        public void GIVEN_a_sadistic_captor_WHEN_selecting_intent_THEN_it_is_torture_whatever_the_regard()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Sadism), relation: 80)
                .Should().Be(CaptiveSceneIntent.Torture);
        }

        [Test]
        public void GIVEN_strong_contempt_and_no_kinks_WHEN_selecting_intent_THEN_it_is_torture()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(), relation: -15).Should().Be(CaptiveSceneIntent.Torture);
        }

        [Test]
        public void GIVEN_warm_regard_and_a_praise_kink_WHEN_selecting_intent_THEN_it_is_reward()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Praise), relation: 20).Should().Be(CaptiveSceneIntent.Reward);
        }

        [Test]
        public void GIVEN_warm_regard_but_no_praise_kink_WHEN_selecting_intent_THEN_it_is_not_reward()
        {
            //Arrange — reward needs the praise kink; without it, warmth lands on personal desire.
            CaptiveEncounterPolicy.SelectIntent(Kinks(), relation: 20).Should().Be(CaptiveSceneIntent.PersonalDesire);
        }

        [Test]
        public void GIVEN_both_a_praise_kink_and_sadism_WHEN_selecting_intent_THEN_torture_wins_by_ladder_order()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Praise, Kink.Sadism), relation: 30)
                .Should().Be(CaptiveSceneIntent.Torture);
        }

        [Test]
        public void GIVEN_a_conditioning_kink_WHEN_selecting_intent_THEN_it_is_training()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Training), relation: 0).Should().Be(CaptiveSceneIntent.Training);
        }

        [Test]
        public void GIVEN_warm_regard_alone_WHEN_selecting_intent_THEN_it_is_personal_desire()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(), relation: 10).Should().Be(CaptiveSceneIntent.PersonalDesire);
        }

        [Test]
        public void GIVEN_a_desire_kink_with_lukewarm_regard_WHEN_selecting_intent_THEN_the_kink_carries_it_to_personal_desire()
        {
            //Arrange — relation 5 (< 10) but a desire kink AND relation >= 0: the || / && rung fires.
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Dominance), relation: 5)
                .Should().Be(CaptiveSceneIntent.PersonalDesire);
        }

        [Test]
        public void GIVEN_the_same_desire_kink_but_negative_regard_WHEN_selecting_intent_THEN_it_falls_through_to_domination()
        {
            //Arrange — relation -5 fails the >= 0 half of the && rung, so PersonalDesire is skipped and
            //          Dominance lands on Domination. This is the precedence the comment in the code locks.
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Dominance), relation: -5)
                .Should().Be(CaptiveSceneIntent.Domination);
        }

        [Test]
        public void GIVEN_an_ownership_kink_outside_the_desire_set_with_lukewarm_regard_WHEN_selecting_intent_THEN_it_is_domination()
        {
            //Arrange — Objectification drives Domination but is NOT in the PersonalDesire kink set,
            //          so at relation 5 it skips the desire rung and lands on Domination.
            CaptiveEncounterPolicy.SelectIntent(Kinks(Kink.Objectification), relation: 5)
                .Should().Be(CaptiveSceneIntent.Domination);
        }

        [Test]
        public void GIVEN_no_kinks_and_neutral_regard_WHEN_selecting_intent_THEN_it_defaults_to_interrogation()
        {
            CaptiveEncounterPolicy.SelectIntent(Kinks(), relation: 0).Should().Be(CaptiveSceneIntent.Interrogation);
        }

        #region private

        private static List<Kink> Kinks(params Kink[] kinks) => new List<Kink>(kinks);

        #endregion
    }
}
