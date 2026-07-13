// Code written by Gabriel Mailhot, 07/07/2026.
// Companion-victim captive scenes, Phase 3: computes the mechanical AFTERMATH once a scene closes, the bond
// damage the host applies to the companion's Trust/Affection toward the player, the revenge grudge magnitude
// against a NAMED captor, and the world-event/chronicle weight. This is the payoff of the whole companion-scene
// pillar: it turns a narrated ordeal into a lasting, verified consequence the player actually feels (a
// companion who trusts them less, a grudge that can be avenged, an atrocity that gets talked about). Get the
// severity-0 short-circuit wrong and a benign scene wrongly scars a bond; get the coerced/named branches wrong
// and the player either escapes a real betrayal's cost, or a faceless bandit wrongly earns a lasting vendetta.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionAbuseConsequencePolicyTests
    {
        // Severity 0 (a Reward/Interrogation-grade scene, per CaptiveIntentRules.CaptiveAbuseSeverity) must
        // produce NOTHING: no bond damage, no grudge, no world-event. Without this short-circuit a benign
        // scene between a captor and a companion could still wrongly scar the companion's trust in the player.
        [Test]
        public void GIVEN_a_non_abusive_scene_severity_zero_WHEN_resolving_THEN_no_consequence_forms()
        {
            CompanionAbuseConsequence c = CompanionAbuseConsequencePolicy.Resolve(0, playerCoerced: false, captorIsNamed: true);

            c.Forms.Should().BeFalse();
            c.BondTrustCrater.Should().Be(0);
            c.CaptorRevengeMagnitude.Should().Be(0);
        }

        // The positive path: a real abuse severity must produce a NEGATIVE bond delta (the companion trusts
        // the player less for failing to save them) and a POSITIVE world-event magnitude (the atrocity gets
        // chronicled). Without this the whole revenge-driver premise of the pillar has nothing to point to.
        [Test]
        public void GIVEN_an_abusive_scene_WHEN_resolving_THEN_the_bond_craters_and_news_carries()
        {
            CompanionAbuseConsequence c = CompanionAbuseConsequencePolicy.Resolve(2, playerCoerced: false, captorIsNamed: false);

            c.Forms.Should().BeTrue();
            c.BondTrustCrater.Should().BeNegative();
            c.BondAffectionCrater.Should().BeNegative();
            c.WorldEventMagnitude.Should().BePositive();
        }

        // A faceless bandit earns no lasting vendetta, even at the WORST severity with the player coerced: a
        // revenge grudge needs a persistent, nameable Hero to hunt down. Without this gate the player would be
        // driven to seek revenge against a nobody they can never actually find again.
        [Test]
        public void GIVEN_a_faceless_bandit_captor_WHEN_resolving_THEN_no_lasting_revenge_grudge_forms()
        {
            CompanionAbuseConsequencePolicy.Resolve(3, playerCoerced: true, captorIsNamed: false)
                .CaptorRevengeMagnitude.Should().Be(0);
        }

        // The mirror of the test above: a NAMED captor (nemesis or lord) must earn a real, positive revenge
        // grudge, and it must DEEPEN with severity so a torture-grade ordeal is avenged harder than a mild one.
        // This magnitude is what the player's eventual revenge against the captor is measured against.
        [Test]
        public void GIVEN_a_named_captor_WHEN_resolving_THEN_a_revenge_grudge_forms_and_deepens_with_severity()
        {
            int light = CompanionAbuseConsequencePolicy.Resolve(1, false, captorIsNamed: true).CaptorRevengeMagnitude;
            int heavy = CompanionAbuseConsequencePolicy.Resolve(3, false, captorIsNamed: true).CaptorRevengeMagnitude;

            light.Should().BePositive();
            heavy.Should().BeGreaterThan(light);
        }

        // Being the FORCED PERPETRATOR (the captor makes the player harm their own companion) is a deeper
        // betrayal than being made to watch (per the production doc); this is the CompanionScenePolicy.PlayerCoerced
        // variant's whole reason for existing as a distinct case, so if this bonus collapsed to zero, coercion
        // would cost the bond no more than passive witnessing did.
        [Test]
        public void GIVEN_the_player_was_forced_to_do_it_WHEN_resolving_THEN_the_bond_craters_deeper_than_merely_watching()
        {
            int watched = CompanionAbuseConsequencePolicy.Resolve(2, playerCoerced: false, captorIsNamed: true).BondTrustCrater;
            int coerced = CompanionAbuseConsequencePolicy.Resolve(2, playerCoerced: true, captorIsNamed: true).BondTrustCrater;

            // Both are negative deltas; the coerced one is a deeper (more negative) wound.
            coerced.Should().BeLessThan(watched);
        }
    }
}
