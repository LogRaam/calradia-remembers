// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the captive-intent classification extracted from ChatViewModel: the memory verb, and which
// intents carry a forced-body toll / a public humiliation.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveIntentRulesTests
    {
        [Test]
        public void GIVEN_each_intent_WHEN_asking_the_verb_THEN_it_reads_in_first_person_past_tense()
        {
            CaptiveIntentRules.Verb(CaptiveSceneIntent.Interrogation).Should().Be("interrogated");
            CaptiveIntentRules.Verb(CaptiveSceneIntent.PersonalDesire).Should().Be("took my pleasure from");
            CaptiveIntentRules.Verb(CaptiveSceneIntent.Reward).Should().Be("rewarded");
        }

        [Test]
        public void GIVEN_a_bandit_menace_intent_WHEN_asking_the_verb_THEN_it_falls_back_to_the_default()
        {
            CaptiveIntentRules.Verb(CaptiveSceneIntent.Extortion).Should().Be("kept as my prisoner");
        }

        [Test]
        public void GIVEN_forced_intents_WHEN_classified_THEN_they_carry_a_forced_body_toll()
        {
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.PersonalDesire).Should().BeTrue();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Domination).Should().BeTrue();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Torture).Should().BeTrue();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Training).Should().BeTrue();
        }

        [Test]
        public void GIVEN_interrogation_or_reward_WHEN_classified_THEN_no_forced_toll_and_no_humiliation()
        {
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Interrogation).Should().BeFalse();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Reward).Should().BeFalse();
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Interrogation).Should().BeFalse();
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Reward).Should().BeFalse();
        }

        [Test]
        public void GIVEN_torture_or_domination_WHEN_classified_THEN_they_are_humiliating()
        {
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Torture).Should().BeTrue();
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Domination).Should().BeTrue();
        }

        // ── CaptiveAbuseSeverity (Grudges pillar, Tier 2) ──────────────────────

        [Test]
        public void GIVEN_reward_WHEN_mapped_to_a_grudge_severity_THEN_it_is_benign_zero()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Reward).Should().Be(0);
        }

        [Test]
        public void GIVEN_interrogation_WHEN_mapped_to_a_grudge_severity_THEN_it_is_mild_one()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Interrogation).Should().Be(1);
        }

        [Test]
        public void GIVEN_domination_training_or_personal_desire_WHEN_mapped_to_a_grudge_severity_THEN_they_are_moderate_two()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Domination).Should().Be(2);
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Training).Should().Be(2);

            // PersonalDesire is folded to the moderate case, not the grievous one: no consent/coercion
            // flag exists yet on a captive scene to tell a genuinely consensual encounter from a forced one.
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.PersonalDesire).Should().Be(2);
        }

        [Test]
        public void GIVEN_torture_WHEN_mapped_to_a_grudge_severity_THEN_it_is_grievous_three()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Torture).Should().Be(3);
        }

        [Test]
        public void GIVEN_a_bandit_menace_intent_WHEN_mapped_to_a_grudge_severity_THEN_it_falls_back_to_zero()
        {
            // These only ever describe the PLAYER's own captivity, never an NPC/companion victim, so
            // there is no grudge holder for them in the current architecture.
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Extortion).Should().Be(0);
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Intimidation).Should().Be(0);
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Revenge).Should().Be(0);
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Reckoning).Should().Be(0);
        }
    }
}
