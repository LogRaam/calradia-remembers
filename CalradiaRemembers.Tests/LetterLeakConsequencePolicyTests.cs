// Code written by Gabriel Mailhot, 25/08/2026.
// Intercepted-letter-leak Inc 2: once a leak fires, these are the two magnitudes its lasting consequences use,
// the grudge the antagonist forms and the renown the player loses as their words become known. This is the ONLY
// place those numbers are pinned, and the courier code that applies them (CourierService.ApplyLeakConsequences)
// trusts this policy for both. If the scaling is wrong, a bitter enemy who reads your secret costs no more than a
// stranger's mild hostility (the leak has no teeth), or a rare, default-off event turns punishing out of proportion.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterLeakConsequencePolicyTests
    {
        // A personal enemy makes the sharpest use of a secret and spreads it most eagerly, so both the grudge and
        // the renown cost must be STRICTLY harsher when the antagonist already held a grudge than when they were a
        // merely hostile lord. If they were equal, the "who holds your secret" distinction the leak is built on
        // would carry no weight.
        [Test]
        public void GIVEN_an_antagonist_who_already_held_a_grudge_WHEN_scaling_the_consequences_THEN_both_are_harsher_than_a_merely_hostile_lord()
        {
            LetterLeakConsequencePolicy.GrudgeMagnitude(true)
                .Should().BeGreaterThan(LetterLeakConsequencePolicy.GrudgeMagnitude(false));
            LetterLeakConsequencePolicy.RenownPenalty(true)
                .Should().BeGreaterThan(LetterLeakConsequencePolicy.RenownPenalty(false));
        }

        // The exact designed values, so a retune is a deliberate edit here and not silent drift: a fired leak is a
        // rare, default-off event, so the grudge is real (intrigue-adjacent) while the renown sting stays small.
        [TestCase(true, LetterLeakConsequencePolicy.GrudgeMagnitudeEnemy, LetterLeakConsequencePolicy.RenownPenaltyEnemy)]
        [TestCase(false, LetterLeakConsequencePolicy.GrudgeMagnitudeHostile, LetterLeakConsequencePolicy.RenownPenaltyHostile)]
        public void GIVEN_the_antagonist_kind_WHEN_scaling_THEN_the_pinned_values_are_returned(bool alreadyHeldGrudge, int expectedGrudge, int expectedRenown)
        {
            LetterLeakConsequencePolicy.GrudgeMagnitude(alreadyHeldGrudge).Should().Be(expectedGrudge);
            LetterLeakConsequencePolicy.RenownPenalty(alreadyHeldGrudge).Should().Be(expectedRenown);
        }

        // The renown sting must stay SMALL: the player's private words leaking to one hostile hand is an
        // embarrassment, not a ruinous public disgrace, and the feature is off by default and rare on top. A large
        // renown swing here would punish an unlucky bandit waylay far out of proportion to the event.
        [Test]
        public void GIVEN_either_antagonist_kind_WHEN_costing_renown_THEN_the_hit_stays_small()
        {
            LetterLeakConsequencePolicy.RenownPenalty(true).Should().BeLessThanOrEqualTo(5);
            LetterLeakConsequencePolicy.RenownPenalty(false).Should().BeLessThanOrEqualTo(5);
            LetterLeakConsequencePolicy.RenownPenalty(false).Should().BeGreaterThan(0); // never a free leak
        }
    }
}
