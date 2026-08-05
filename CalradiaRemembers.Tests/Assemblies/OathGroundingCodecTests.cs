// Code written by Gabriel Mailhot, 04/08/2026.
// Documents the composite grounding swear_oath uses to carry (oath kind, target value) through the single
// TargetHint slot every other resolution kind reuses, mirroring ArrangeMarriageGroundingCodecTests'/
// FiefSwapGroundingCodecTests' own coverage. The ONE deliberate difference from those two: this codec's RIGHT
// half (the target value) may legitimately be blank (a protect oath names no target beyond the player), so its
// own round-trip rule differs and needs its own tests, not just a copy of theirs. The stake throughout: a codec
// that fails to round-trip could leave CouncilLift.SettleSwearOath tracking the WRONG oath kind, or refusing a
// perfectly valid protect oath because its target half is empty.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class OathGroundingCodecTests
    {
        // The baseline round-trip: what RecordCouncilResolution composes for a pay_gold oath, SettleSwearOath
        // must read back as the exact same kind and amount, or the lift tracks the wrong oath.
        [Test]
        public void GIVEN_an_oath_kind_and_a_target_value_WHEN_composed_and_decomposed_THEN_both_survive_unchanged()
        {
            string composite = OathGroundingCodec.Compose("pay_gold", "300");

            OathGroundingCodec.TryDecompose(composite, out string oathKind, out string targetValue).Should().BeTrue();
            oathKind.Should().Be("pay_gold");
            targetValue.Should().Be("300");
        }

        // The stake this codec exists for: unlike ArrangeMarriageGroundingCodec/FiefSwapGroundingCodec (where
        // BOTH halves are mandatory), a protect oath legitimately names no second value at all. If a blank
        // target value failed the round-trip here, every protect oath the model ever proposed would be refused
        // at the lift, exactly the "no valid sum was named" failure a protect oath must never hit.
        [Test]
        public void GIVEN_a_blank_target_value_WHEN_composed_and_decomposed_THEN_it_still_succeeds_with_an_empty_target()
        {
            string composite = OathGroundingCodec.Compose("protect", "");

            OathGroundingCodec.TryDecompose(composite, out string oathKind, out string targetValue).Should().BeTrue();
            oathKind.Should().Be("protect");
            targetValue.Should().Be("");
        }

        // A null target value at compose time (protect's own caller never resolves a second value at all) must
        // behave identically to an explicit blank one, never throw and never fail to decompose.
        [Test]
        public void GIVEN_a_null_target_value_WHEN_composed_and_decomposed_THEN_it_still_succeeds_with_an_empty_target()
        {
            string composite = OathGroundingCodec.Compose("protect", null);

            OathGroundingCodec.TryDecompose(composite, out string oathKind, out string targetValue).Should().BeTrue();
            oathKind.Should().Be("protect");
            targetValue.Should().Be("");
        }

        // UNLIKE the target value, the oath kind itself is ALWAYS mandatory: an oath with no kind named is not
        // an oath at all, and the lift must refuse it honestly (CouncilLift.SettleSwearOath's "no oath kind was
        // named") rather than guess one.
        [Test]
        public void GIVEN_a_blank_oath_kind_WHEN_decomposed_THEN_it_fails_even_if_a_target_value_is_present()
        {
            OathGroundingCodec.TryDecompose("|300", out string oathKind, out string targetValue).Should().BeFalse();
            oathKind.Should().BeNull();
            targetValue.Should().BeNull();
        }

        // A null or blank composite (no [RESOLUTION] ever recorded a TargetHint, an older/foreign entry) must
        // never decompose: inventing an oath kind from nothing is exactly the silent-guess class this codec
        // exists to avoid.
        [Test]
        public void GIVEN_a_null_or_blank_composite_WHEN_decomposed_THEN_it_fails()
        {
            OathGroundingCodec.TryDecompose(null, out string a, out string b).Should().BeFalse();
            a.Should().BeNull();
            b.Should().BeNull();

            OathGroundingCodec.TryDecompose("", out a, out b).Should().BeFalse();
            OathGroundingCodec.TryDecompose("   ", out a, out b).Should().BeFalse();
        }

        // A composite with no delimiter at all (a stray TargetHint from a DIFFERENT resolution kind, e.g. a
        // plain faction or settlement name) must fail rather than be misread as a bare oath kind with no target.
        [Test]
        public void GIVEN_a_composite_with_no_delimiter_WHEN_decomposed_THEN_it_fails()
        {
            OathGroundingCodec.TryDecompose("pay_gold", out string a, out string b).Should().BeFalse();
            a.Should().BeNull();
            b.Should().BeNull();
        }

        // An odd input: a target value that itself happens to contain the delimiter character must still
        // round-trip whole on the right-hand side (only the FIRST delimiter splits), never truncated.
        [Test]
        public void GIVEN_the_target_value_itself_contains_the_delimiter_WHEN_composed_and_decomposed_THEN_it_survives_whole()
        {
            string composite = OathGroundingCodec.Compose("keep_peace", "Vla|ndia");

            OathGroundingCodec.TryDecompose(composite, out string oathKind, out string targetValue).Should().BeTrue();
            oathKind.Should().Be("keep_peace");
            targetValue.Should().Be("Vla|ndia");
        }
    }
}
