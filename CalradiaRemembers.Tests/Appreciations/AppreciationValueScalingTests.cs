// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar: the denar-value-to-magnitude mapping shared by the GiftReceived/GoldReceived
// live plant sites.
// Feeds AppreciationBehavior.TryPlant, which decides whether the resulting magnitude clears
// AppreciationPolicy's formation floor. Wrong at the low end and a real hand-off silently forms no
// kindness at all, contradicting the mapping's own "the ACT of giving is never worth zero" rule; wrong at
// the high end and a single huge gift maxes out WarmthWord's top band and can crowd out other standing
// kindnesses via the per-NPC eviction cap.

#region

using CalradiaRemembers.Logic.Appreciations;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Appreciations
{
    [TestFixture]
    public sealed class AppreciationValueScalingTests
    {
        // Stake: pins the core deterministic ratio ExecuteGiveItem/ExecuteTakeGold rely on to turn a real
        // denar amount into the qualitative warmth scale AppreciationNarrator narrates; a silent retune of
        // the divisor would shift every future gift's felt magnitude without the encyclopedia line ever
        // showing a number to notice it by.
        [Test]
        public void GIVEN_a_middling_value_WHEN_scaling_THEN_it_divides_by_the_denars_per_point_constant()
        {
            AppreciationValueScaling.FromValue(3000).Should().Be(30);
        }

        // Stake: the production doc is explicit ("the ACT of giving is never worth zero, only worth
        // little"); without this floor a token 1-denar gift would scale to a magnitude too small to clear
        // AppreciationPolicy's formation floor, so the kindness would silently never form at all.
        [Test]
        public void GIVEN_a_tiny_value_WHEN_scaling_THEN_it_floors_at_the_minimum_magnitude()
        {
            AppreciationValueScaling.FromValue(1).Should().Be(AppreciationValueScaling.MinMagnitude);
        }

        // Stake: extends the same floor rule to degenerate/defensive inputs; a caller bug passing 0 or a
        // negative value must never propagate a nonsensical zero or negative magnitude into
        // AppreciationPolicy.ShouldForm, which trusts this method's output is already in a sane range.
        [Test]
        public void GIVEN_a_zero_or_negative_value_WHEN_scaling_THEN_it_still_floors_at_the_minimum_magnitude()
        {
            AppreciationValueScaling.FromValue(0).Should().Be(AppreciationValueScaling.MinMagnitude);
            AppreciationValueScaling.FromValue(-500).Should().Be(AppreciationValueScaling.MinMagnitude);
        }

        // Stake: the production doc is explicit ("no single hand-off reads as an overwhelming debt of
        // gratitude"); without this ceiling one extravagant gift could instantly max WarmthWord's top band
        // and, via AppreciationPolicy.TryAdmit's per-NPC cap, crowd out every other kindness the player
        // had earned with that NPC.
        [Test]
        public void GIVEN_a_huge_value_WHEN_scaling_THEN_it_ceilings_at_the_maximum_magnitude()
        {
            AppreciationValueScaling.FromValue(1000000).Should().Be(AppreciationValueScaling.MaxMagnitude);
        }
    }
}
