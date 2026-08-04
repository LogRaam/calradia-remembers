// Code written by Gabriel Mailhot, 04/08/2026.
// Documents the composite grounding swap_fiefs uses to carry TWO fief names through the single TargetHint slot
// every other resolution kind reuses, mirroring ArrangeMarriageGroundingCodecTests' own coverage. The stake
// throughout: a codec that fails to round-trip could leave CouncilLift.SettleSwapFiefs resolving the wrong pair
// of fiefs, or worse, transferring one side of the trade while silently dropping the other, exactly the
// half-a-swap outcome the whole atomic design exists to prevent.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class FiefSwapGroundingCodecTests
    {
        // The baseline round-trip: what RecordCouncilResolution composes, SettleSwapFiefs must read back as the
        // exact same two fief names, or the lift trades the wrong pair (or nothing at all).
        [Test]
        public void GIVEN_two_ordinary_fief_names_WHEN_composed_and_decomposed_THEN_both_survive_unchanged()
        {
            string composite = FiefSwapGroundingCodec.Compose("Pravend", "Rhojen");

            FiefSwapGroundingCodec.TryDecompose(composite, out string playerFief, out string targetFief).Should().BeTrue();
            playerFief.Should().Be("Pravend");
            targetFief.Should().Be("Rhojen");
        }

        // A null composite (no [RESOLUTION] ever recorded a TargetHint, an older/foreign entry) must never
        // decompose: inventing a pair of fiefs from nothing is exactly the silent-guess class this codec exists
        // to avoid.
        [Test]
        public void GIVEN_a_null_or_blank_composite_WHEN_decomposed_THEN_it_fails()
        {
            FiefSwapGroundingCodec.TryDecompose(null, out string a, out string b).Should().BeFalse();
            a.Should().BeNull();
            b.Should().BeNull();

            FiefSwapGroundingCodec.TryDecompose("", out a, out b).Should().BeFalse();
            FiefSwapGroundingCodec.TryDecompose("   ", out a, out b).Should().BeFalse();
        }

        // A composite with no delimiter at all (a stray TargetHint from a DIFFERENT resolution kind, e.g. a
        // plain settlement or faction name) must fail rather than be mis-split into a player fief with no
        // counterpart.
        [Test]
        public void GIVEN_a_composite_with_no_delimiter_WHEN_decomposed_THEN_it_fails()
        {
            FiefSwapGroundingCodec.TryDecompose("Pravend", out string a, out string b).Should().BeFalse();
            a.Should().BeNull();
            b.Should().BeNull();
        }

        // Either half left blank (the model named only one fief, or RecordCouncilResolution composed a partial
        // pair) must fail the round-trip: the lift must refuse the pledge honestly rather than resolve a swap
        // against an empty fief name, the exact discipline that keeps the trade atomic.
        [Test]
        public void GIVEN_either_half_is_blank_WHEN_decomposed_THEN_it_fails()
        {
            FiefSwapGroundingCodec.TryDecompose("Pravend|", out string a, out string b).Should().BeFalse();
            FiefSwapGroundingCodec.TryDecompose("|Rhojen", out a, out b).Should().BeFalse();
            FiefSwapGroundingCodec.TryDecompose("|", out a, out b).Should().BeFalse();
        }

        // An odd input: a target fief name that itself happens to contain the delimiter character must still
        // round-trip whole on the right-hand side (only the FIRST delimiter splits), never truncated at the
        // second occurrence.
        [Test]
        public void GIVEN_the_target_fief_name_itself_contains_the_delimiter_WHEN_composed_and_decomposed_THEN_it_survives_whole()
        {
            string composite = FiefSwapGroundingCodec.Compose("Pravend", "Rho|jen");

            FiefSwapGroundingCodec.TryDecompose(composite, out string playerFief, out string targetFief).Should().BeTrue();
            playerFief.Should().Be("Pravend");
            targetFief.Should().Be("Rho|jen");
        }

        // A null half at compose time (the caller only resolved one field) must still produce a decomposable-or-
        // honestly-failing string, never throw: Compose is called unconditionally by RecordCouncilResolution
        // whatever the model actually named.
        [Test]
        public void GIVEN_a_null_half_WHEN_composed_THEN_it_does_not_throw_and_still_fails_to_decompose()
        {
            string composite = FiefSwapGroundingCodec.Compose(null, "Rhojen");

            FiefSwapGroundingCodec.TryDecompose(composite, out string a, out string b).Should().BeFalse();
        }
    }
}
