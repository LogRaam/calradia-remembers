// Code written by Gabriel Mailhot, 07/07/2026.

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
        [Test]
        public void GIVEN_a_non_abusive_scene_severity_zero_WHEN_resolving_THEN_no_consequence_forms()
        {
            CompanionAbuseConsequence c = CompanionAbuseConsequencePolicy.Resolve(0, playerCoerced: false, captorIsNamed: true);

            c.Forms.Should().BeFalse();
            c.BondTrustCrater.Should().Be(0);
            c.CaptorRevengeMagnitude.Should().Be(0);
        }

        [Test]
        public void GIVEN_an_abusive_scene_WHEN_resolving_THEN_the_bond_craters_and_news_carries()
        {
            CompanionAbuseConsequence c = CompanionAbuseConsequencePolicy.Resolve(2, playerCoerced: false, captorIsNamed: false);

            c.Forms.Should().BeTrue();
            c.BondTrustCrater.Should().BeNegative();
            c.BondAffectionCrater.Should().BeNegative();
            c.WorldEventMagnitude.Should().BePositive();
        }

        [Test]
        public void GIVEN_a_faceless_bandit_captor_WHEN_resolving_THEN_no_lasting_revenge_grudge_forms()
        {
            CompanionAbuseConsequencePolicy.Resolve(3, playerCoerced: true, captorIsNamed: false)
                .CaptorRevengeMagnitude.Should().Be(0);
        }

        [Test]
        public void GIVEN_a_named_captor_WHEN_resolving_THEN_a_revenge_grudge_forms_and_deepens_with_severity()
        {
            int light = CompanionAbuseConsequencePolicy.Resolve(1, false, captorIsNamed: true).CaptorRevengeMagnitude;
            int heavy = CompanionAbuseConsequencePolicy.Resolve(3, false, captorIsNamed: true).CaptorRevengeMagnitude;

            light.Should().BePositive();
            heavy.Should().BeGreaterThan(light);
        }

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
