// Code written by Gabriel Mailhot, 04/08/2026.
// Documents the composite grounding arrange_marriage uses to carry TWO kin names through the single TargetHint
// slot every other resolution kind reuses. The stake throughout: a codec that fails to round-trip silently loses
// one of the two kin the council pledged to wed, and CouncilLift.SettleArrangeMarriage would then either resolve
// the wrong pair or refuse a match the table genuinely struck, exactly the empty-promise class this whole design
// exists to prevent.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class ArrangeMarriageGroundingCodecTests
    {
        // The baseline round-trip: what RecordCouncilResolution composes, SettleArrangeMarriage must read back
        // as the exact same two names, or the lift weds the wrong kin (or nobody at all).
        [Test]
        public void GIVEN_two_ordinary_kin_names_WHEN_composed_and_decomposed_THEN_both_survive_unchanged()
        {
            string composite = ArrangeMarriageGroundingCodec.Compose("Elvira", "Boyar Sevin");

            ArrangeMarriageGroundingCodec.TryDecompose(composite, out string playerKin, out string targetKin).Should().BeTrue();
            playerKin.Should().Be("Elvira");
            targetKin.Should().Be("Boyar Sevin");
        }

        // A null composite (no [RESOLUTION] ever recorded a TargetHint, an older/foreign entry) must never
        // decompose: inventing a pair of names from nothing is exactly the silent-guess class this codec exists
        // to avoid.
        [Test]
        public void GIVEN_a_null_or_blank_composite_WHEN_decomposed_THEN_it_fails()
        {
            ArrangeMarriageGroundingCodec.TryDecompose(null, out string a, out string b).Should().BeFalse();
            a.Should().BeNull();
            b.Should().BeNull();

            ArrangeMarriageGroundingCodec.TryDecompose("", out a, out b).Should().BeFalse();
            ArrangeMarriageGroundingCodec.TryDecompose("   ", out a, out b).Should().BeFalse();
        }

        // A composite with no delimiter at all (a stray TargetHint from a DIFFERENT resolution kind, e.g. a
        // plain settlement or faction name) must fail rather than be mis-split into a player kin with no target.
        [Test]
        public void GIVEN_a_composite_with_no_delimiter_WHEN_decomposed_THEN_it_fails()
        {
            ArrangeMarriageGroundingCodec.TryDecompose("Pravend", out string a, out string b).Should().BeFalse();
            a.Should().BeNull();
            b.Should().BeNull();
        }

        // Either half left blank (the model named only one kin, or RecordCouncilResolution composed a partial
        // pair) must fail the round-trip: the lift must refuse the pledge honestly (NoPlayerKin/NoTargetKin)
        // rather than resolve a match against an empty name.
        [Test]
        public void GIVEN_either_half_is_blank_WHEN_decomposed_THEN_it_fails()
        {
            ArrangeMarriageGroundingCodec.TryDecompose("Elvira|", out string a, out string b).Should().BeFalse();
            ArrangeMarriageGroundingCodec.TryDecompose("|Boyar Sevin", out a, out b).Should().BeFalse();
            ArrangeMarriageGroundingCodec.TryDecompose("|", out a, out b).Should().BeFalse();
        }

        // An odd input: a target kin name that itself happens to contain the delimiter character must still
        // round-trip whole on the right-hand side (only the FIRST delimiter splits), never truncated at the
        // second occurrence.
        [Test]
        public void GIVEN_the_target_kin_name_itself_contains_the_delimiter_WHEN_composed_and_decomposed_THEN_it_survives_whole()
        {
            string composite = ArrangeMarriageGroundingCodec.Compose("Elvira", "Boyar|Sevin");

            ArrangeMarriageGroundingCodec.TryDecompose(composite, out string playerKin, out string targetKin).Should().BeTrue();
            playerKin.Should().Be("Elvira");
            targetKin.Should().Be("Boyar|Sevin");
        }

        // A null half at compose time (the caller only resolved one field) must still produce a decomposable-or-
        // honestly-failing string, never throw: Compose is called unconditionally by RecordCouncilResolution
        // whatever the model actually named.
        [Test]
        public void GIVEN_a_null_half_WHEN_composed_THEN_it_does_not_throw_and_still_fails_to_decompose()
        {
            string composite = ArrangeMarriageGroundingCodec.Compose(null, "Boyar Sevin");

            ArrangeMarriageGroundingCodec.TryDecompose(composite, out string a, out string b).Should().BeFalse();
        }
    }
}
