// Code written by Gabriel Mailhot, 12/07/2026.
// Documents the distance bucketing: monotonic tiers 0..3, boundaries fall into the higher band, and the tier
// at Tier3 and beyond is the "far" tier the RSVP relevance gate keys on.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class AssemblyDistanceTiersTests
    {
        // Pins the tier boundaries as inclusive-at-the-top-of-the-lower-band (a distance exactly at TierNDistance
        // already belongs to the higher tier), and that nothing beyond Tier3 ever exceeds tier 3.
        [Test]
        public void GIVEN_distances_in_each_band_WHEN_bucketed_THEN_they_map_to_the_expected_tier()
        {
            AssemblyDistanceTiers.Of(0f).Should().Be(0);
            AssemblyDistanceTiers.Of(AssemblyDistanceTiers.Tier1Distance - 1f).Should().Be(0);
            AssemblyDistanceTiers.Of(AssemblyDistanceTiers.Tier1Distance).Should().Be(1);
            AssemblyDistanceTiers.Of(AssemblyDistanceTiers.Tier2Distance).Should().Be(2);
            AssemblyDistanceTiers.Of(AssemblyDistanceTiers.Tier3Distance).Should().Be(3);
            AssemblyDistanceTiers.Of(10_000f).Should().Be(3);
        }

        // Two independently tunable constants (Tier3Distance here, FarDistanceTier in AssemblyRsvpPolicy) must
        // never drift apart, or a far-tier distance could stop tripping the "not worth the travel" gate. Mirrors
        // the shared-threshold pattern in PlayerDivorceConsentPolicyTests.
        [Test]
        public void GIVEN_the_far_tier_WHEN_compared_to_the_rsvp_gate_THEN_they_agree_that_tier_3_is_far()
        {
            // The relevance gate treats FarDistanceTier and beyond as "far"; this pins the two in step.
            AssemblyDistanceTiers.Of(AssemblyDistanceTiers.Tier3Distance)
                .Should().BeGreaterThanOrEqualTo(AssemblyRsvpPolicy.FarDistanceTier);
        }
    }
}
