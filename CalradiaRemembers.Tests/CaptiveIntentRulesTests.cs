// Code written by Gabriel Mailhot, 24/06/2026.
// Documents the captive-intent classification extracted from ChatViewModel: the memory verb, and which
// intents carry a forced-body toll / a public humiliation.
//
// WHY IT MATTERS: Verb feeds the first-person past-tense scene memory summary the NPC recalls later, so a
// wrong mapping puts the wrong act in a captor's own memory of what they did. IsForcedSexual/IsHumiliating
// gate mechanical consequences (the body toll and the public-shame framing) onto only the intents that
// actually warrant them, so a gentle Reward or an official Interrogation never wrongly costs the player HP or
// reads as a humiliation. CaptiveAbuseSeverity feeds the Grudges pillar's Tier 2 CaptiveAbuse rule: a wrong
// severity either plants a grudge for a benign scene or lets a grievous one pass with none.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveIntentRulesTests
    {
        // The verb lands verbatim in the NPC's own scene memory summary; a wrong mapping puts a false act in
        // their recollection (e.g. an interrogation remembered as a pleasure-taking), which then colours every
        // future conversation that references the memory.
        [Test]
        public void GIVEN_each_intent_WHEN_asking_the_verb_THEN_it_reads_in_first_person_past_tense()
        {
            CaptiveIntentRules.Verb(CaptiveSceneIntent.Interrogation).Should().Be("interrogated");
            CaptiveIntentRules.Verb(CaptiveSceneIntent.PersonalDesire).Should().Be("took my pleasure from");
            CaptiveIntentRules.Verb(CaptiveSceneIntent.Reward).Should().Be("rewarded");
        }

        // Guards the switch's default arm: a bandit-menace intent (Extortion here) has no dedicated verb case,
        // so this pins that it falls back to the generic phrase instead of throwing or silently returning null
        // into the memory summary.
        [Test]
        public void GIVEN_a_bandit_menace_intent_WHEN_asking_the_verb_THEN_it_falls_back_to_the_default()
        {
            CaptiveIntentRules.Verb(CaptiveSceneIntent.Extortion).Should().Be("kept as my prisoner");
        }

        // IsForcedSexual gates a real body toll (lost HP) onto the scene. Reward is gentle and Interrogation
        // is not inherently sexual (per the production doc), so only these force-by-violence intents may cost
        // the player health; missing one here means a genuinely forced scene costs nothing.
        [Test]
        public void GIVEN_forced_intents_WHEN_classified_THEN_they_carry_a_forced_body_toll()
        {
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.PersonalDesire).Should().BeTrue();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Domination).Should().BeTrue();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Torture).Should().BeTrue();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Training).Should().BeTrue();
        }

        // The negative space of both flags matters just as much as the positive: Interrogation is official and
        // Reward is favour (per the production doc), so a bug that over-eagerly flags either as forced or
        // humiliating would wrongly cost HP or shame the player for a scene that did neither.
        [Test]
        public void GIVEN_interrogation_or_reward_WHEN_classified_THEN_no_forced_toll_and_no_humiliation()
        {
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Interrogation).Should().BeFalse();
            CaptiveIntentRules.IsForcedSexual(CaptiveSceneIntent.Reward).Should().BeFalse();
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Interrogation).Should().BeFalse();
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Reward).Should().BeFalse();
        }

        // IsHumiliating drives the public-shame framing of a scene; Torture and Domination are the intents
        // that constitute the abasement the production doc describes, so missing either would understate a
        // scene the player experienced as public humiliation.
        [Test]
        public void GIVEN_torture_or_domination_WHEN_classified_THEN_they_are_humiliating()
        {
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Torture).Should().BeTrue();
            CaptiveIntentRules.IsHumiliating(CaptiveSceneIntent.Domination).Should().BeTrue();
        }

        // ── CaptiveAbuseSeverity (Grudges pillar, Tier 2) ──────────────────────

        // Severity 0 means NO grudge forms downstream in Tier2GrudgeRules.CaptiveAbuseGrudge: a favour scene
        // must never plant a grievance, or the player would be resented for being kind to their own captive.
        [Test]
        public void GIVEN_reward_WHEN_mapped_to_a_grudge_severity_THEN_it_is_benign_zero()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Reward).Should().Be(0);
        }

        // Pins the mild end of the 0..3 scale: an official interrogation is not benign but is far short of the
        // grievous tier, so it must not weigh as heavily against the player as a genuinely forced scene.
        [Test]
        public void GIVEN_interrogation_WHEN_mapped_to_a_grudge_severity_THEN_it_is_mild_one()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Interrogation).Should().Be(1);
        }

        // PersonalDesire is deliberately folded into the SAME moderate tier as Domination/Training rather than
        // assumed grievous, because (per the production doc) no consent/coercion flag exists yet on a captive
        // scene to tell a genuinely consensual encounter from a forced one: a documented gap, not an oversight.
        [Test]
        public void GIVEN_domination_training_or_personal_desire_WHEN_mapped_to_a_grudge_severity_THEN_they_are_moderate_two()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Domination).Should().Be(2);
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Training).Should().Be(2);

            // PersonalDesire is folded to the moderate case, not the grievous one: no consent/coercion
            // flag exists yet on a captive scene to tell a genuinely consensual encounter from a forced one.
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.PersonalDesire).Should().Be(2);
        }

        // The top of the scale: Torture must land at the maximum severity, or the heaviest possible scene
        // would under-weigh against the player in the Tier 2 grudge engine.
        [Test]
        public void GIVEN_torture_WHEN_mapped_to_a_grudge_severity_THEN_it_is_grievous_three()
        {
            CaptiveIntentRules.CaptiveAbuseSeverity(CaptiveSceneIntent.Torture).Should().Be(3);
        }

        // These intents only ever describe the PLAYER's OWN captivity (never an NPC/companion victim), so
        // there is no grudge holder for them in the current architecture; pins that they fall back to 0
        // rather than the switch's default accidentally attaching a grudge severity to a bandit-menace scene.
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
