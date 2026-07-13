// Code written by Gabriel Mailhot, 26/06/2026.
// Documents that a discovered bastard cuts deeper in a strictly-monogamous culture and barely stings in a
// permissive one — reusing the jealousy system's per-culture outrage.
//
// WHY IT MATTERS: BastardBehavior calls Scale() to size the Renown/Influence hit and the scandal's rumour
// magnitude when a hidden bastard is discovered. It deliberately reuses JealousyOutrage (the SAME per-culture
// bar the jealousy system already judges affairs by) instead of its own scale, so a culture's stance on
// infidelity reads consistently across both systems. Get SeverityPercent wrong and a bastard scandal either
// trivializes a grave dishonour in a strict culture, or punishes a permissive one as harshly as a strict one.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class BastardScandalPolicyTests
    {
        // GravePercent (150%) is the strict-culture case a proven bastard should cost the most in: a wrong
        // value here would undersell the dishonour to a culture that, per the jealousy system, treats it gravely.
        [Test]
        public void GIVEN_a_strictly_monogamous_culture_WHEN_pricing_a_scandal_THEN_it_is_grave()
        {
            BastardScandalPolicy.SeverityPercent(JealousyOutrage.High)
                .Should().Be(BastardScandalPolicy.GravePercent);
        }

        // MildPercent (50%) is the other extreme: a permissive culture's Renown/Influence hit must land far
        // softer than a strict culture's, or the whole point of reusing per-culture outrage is lost.
        [Test]
        public void GIVEN_a_permissive_culture_WHEN_pricing_a_scandal_THEN_it_mostly_shrugs()
        {
            BastardScandalPolicy.SeverityPercent(JealousyOutrage.Low)
                .Should().Be(BastardScandalPolicy.MildPercent);
        }

        // Confirms Scale() actually applies SeverityPercent as a multiplier on the real Renown/Influence base
        // values BastardBehavior passes in, not just returning the percent itself.
        [Test]
        public void GIVEN_a_grave_culture_WHEN_scaling_a_cost_THEN_it_is_amplified()
        {
            //Arrange — High outrage at 150 %.
            BastardScandalPolicy.Scale(10, JealousyOutrage.High).Should().Be(15);
        }

        // The reduction side of the same multiplier: a permissive culture must genuinely soften the base cost,
        // not merely leave the percent field unused.
        [Test]
        public void GIVEN_a_permissive_culture_WHEN_scaling_a_cost_THEN_it_is_softened()
        {
            BastardScandalPolicy.Scale(10, JealousyOutrage.Low).Should().Be(5);
        }

        // Medium outrage is the reference point (100%, no scaling either way) every strict/permissive case is
        // judged relative to.
        [Test]
        public void GIVEN_a_neutral_culture_WHEN_scaling_a_cost_THEN_it_is_unchanged()
        {
            BastardScandalPolicy.Scale(10, JealousyOutrage.Medium).Should().Be(10);
        }

        // Integer rounding at 50% on a small base can hit exactly 0, which would let a scandal cost NOTHING
        // even in a culture that still cares somewhat. Scale() floors a positive base at 1 to guard against that.
        [Test]
        public void GIVEN_a_positive_base_in_a_permissive_culture_WHEN_scaling_THEN_it_never_vanishes()
        {
            //Arrange — 1 * 50 % rounds to 0, but a scandal always costs at least a little.
            BastardScandalPolicy.Scale(1, JealousyOutrage.Low).Should().Be(1);
        }
    }
}
