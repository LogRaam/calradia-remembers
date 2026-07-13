// Code written by Gabriel Mailhot, 05/07/2026.
// Nemesis pillar: the escaping-a-nemesis-captor arc. Gabriel's explicit ruling ("je laisserais une chance
// quand meme", ROADMAP) was that a nemesis is NOT a perfect prison: the player always keeps some chance to
// slip away, but that chance decays with every recapture as he learns to guard the player more carefully.
//
// WHY IT MATTERS: EscapeChance replaces the flat scene-escape roll ONLY when the captor is the tracked
// nemesis (ordinary captors keep the flat chance); VanillaDripSuppressChance fully suppresses the passive
// vanilla escape drip so a nemesis captivity can't just be waited out. Get the floor wrong and the nemesis
// becomes either a hopeless cage (breaking Gabriel's ruling) or trivially escapable (no "you slipped me once,
// not again" escalation).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisEscapeChancePolicyTests
    {
        // The FIRST nemesis captivity matches the ordinary scene chance (per the TUNE comment): a fresh
        // nemesis is not yet the escalating threat he becomes, so the arc must start at parity, not already tightened.
        [Test]
        public void GIVEN_a_first_captivity_with_no_prior_recapture_WHEN_resolving_the_chance_THEN_it_is_the_base_chance()
        {
            NemesisEscapeChancePolicy.EscapeChance(0).Should().Be(NemesisEscapeChancePolicy.BaseEscapeChance);
        }

        // Pins the exact per-recapture step, so "you slipped me once, not again" is a real, measured tightening
        // and not just a vague notion of "harder now".
        [Test]
        public void GIVEN_one_prior_recapture_WHEN_resolving_the_chance_THEN_it_has_dropped_by_one_step()
        {
            NemesisEscapeChancePolicy.EscapeChance(1)
                .Should().BeApproximately(NemesisEscapeChancePolicy.BaseEscapeChance - NemesisEscapeChancePolicy.DropPerRecapture, 1e-9);
        }

        // The captor "learns": a recapture must never make the NEXT escape easier, or the escalating-arc
        // fantasy would break the first time the curve dipped back up.
        [Test]
        public void GIVEN_growing_recapture_counts_WHEN_resolving_the_chance_THEN_it_never_increases()
        {
            double previous = 1.0;
            for (int recaptures = 0; recaptures <= 10; recaptures++)
            {
                double chance = NemesisEscapeChancePolicy.EscapeChance(recaptures);
                chance.Should().BeLessThanOrEqualTo(previous);
                previous = chance;
            }
        }

        // This IS Gabriel's explicit ruling made concrete: even the most many-times-recaptured nemesis must
        // leave a real, non-zero chance. Without this floor the escape mechanic could decay into a mathematically
        // perfect cage, which he specifically rejected.
        [Test]
        public void GIVEN_many_recaptures_WHEN_resolving_the_chance_THEN_it_settles_on_the_floor_but_never_zero()
        {
            double chance = NemesisEscapeChancePolicy.EscapeChance(1000);

            chance.Should().Be(NemesisEscapeChancePolicy.MinEscapeChance);
            chance.Should().BeGreaterThan(0.0);
        }

        // Defensive: the engine should never pass a negative recapture count, but the arc must not break (or
        // worse, inflate the chance past the base) if it does.
        [Test]
        public void GIVEN_a_negative_recapture_count_WHEN_resolving_the_chance_THEN_it_is_treated_as_a_first_captivity()
        {
            NemesisEscapeChancePolicy.EscapeChance(-5).Should().Be(NemesisEscapeChancePolicy.BaseEscapeChance);
        }

        // The TUNE comment is explicit that the base is deliberately BELOW full suppression ("about one offered
        // escape in five still stands"): a fresh nemesis must not already act as a perfect passive cage before he
        // has earned that reputation through recaptures.
        [Test]
        public void GIVEN_a_first_captivity_WHEN_resolving_the_vanilla_drip_suppress_THEN_it_is_the_base_and_leaves_a_real_chance()
        {
            double suppress = NemesisEscapeChancePolicy.VanillaDripSuppressChance(0);

            suppress.Should().Be(NemesisEscapeChancePolicy.VanillaDripSuppressBase);
            suppress.Should().BeLessThan(1.0, "a fresh nemesis never fully suppresses the passive escape");
        }

        // Mirrors the active-escape step test: pins the exact per-recapture tightening of the passive drip
        // suppression, the mirror mechanic to EscapeChance's own decay.
        [Test]
        public void GIVEN_one_prior_recapture_WHEN_resolving_the_vanilla_drip_suppress_THEN_it_has_tightened_by_one_step()
        {
            NemesisEscapeChancePolicy.VanillaDripSuppressChance(1)
                .Should().BeApproximately(NemesisEscapeChancePolicy.VanillaDripSuppressBase + NemesisEscapeChancePolicy.VanillaDripSuppressPerRecapture, 1e-9);
        }

        // The suppression must climb monotonically toward its cap but stay strictly under 1.0 at every step:
        // per the doc comment, "the player is never left to wait out days with no exit", so a passive escape
        // must always remain slimly possible no matter how many times he has been recaptured.
        [Test]
        public void GIVEN_growing_recapture_counts_WHEN_resolving_the_vanilla_drip_suppress_THEN_it_never_decreases_and_never_fully_cages()
        {
            double previous = 0.0;
            for (int recaptures = 0; recaptures <= 20; recaptures++)
            {
                double suppress = NemesisEscapeChancePolicy.VanillaDripSuppressChance(recaptures);
                suppress.Should().BeGreaterThanOrEqualTo(previous);
                suppress.Should().BeLessThanOrEqualTo(NemesisEscapeChancePolicy.VanillaDripSuppressMax);
                suppress.Should().BeLessThan(1.0);
                previous = suppress;
            }
        }

        // Defensive, mirrors the EscapeChance guard: a negative count must never over-suppress the passive drip.
        [Test]
        public void GIVEN_a_negative_recapture_count_WHEN_resolving_the_vanilla_drip_suppress_THEN_it_is_treated_as_a_first_captivity()
        {
            NemesisEscapeChancePolicy.VanillaDripSuppressChance(-3).Should().Be(NemesisEscapeChancePolicy.VanillaDripSuppressBase);
        }
    }
}
