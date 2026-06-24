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
    }
}
