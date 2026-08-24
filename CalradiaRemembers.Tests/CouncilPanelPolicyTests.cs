// Code written by Gabriel Mailhot, 24/08/2026.
// Council Tier 1 reconciliation: the Engagements panel and the end-of-council memory seal are both driven from the
// REAL resolution ledger now, so this policy is the single arbiter of what the player sees on the panel and what
// gets remembered. What breaks if it is wrong: a withdrawn motion lingers on the panel as if still standing; a
// committed decision shows the wrong status word; or a seal double-writes a memory for a pledge kind that already
// recorded its own at the lift (SwearOath via CommitmentBehavior, PledgeAgainst via LaunchPledge), so the NPC ends
// up with two memories of the same act.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CouncilPanelPolicyTests
    {
        // A motion withdrawn during the session was taken back before it could commit, so it must vanish from the
        // panel; leaving it visible would tell the player a decision stands when the table already dropped it.
        [Test]
        public void GIVEN_a_reversed_motion_WHEN_deciding_panel_visibility_THEN_it_is_hidden()
        {
            CouncilPanelPolicy.ShouldShow(ResolutionStatus.Reversed).Should().BeFalse();
        }

        // Everything else the table raised is part of the record the player should see: still under discussion,
        // agreed, or (after the lift) committed or failed. Hiding any of these would make the panel lie by omission.
        [TestCase(ResolutionStatus.Proposed)]
        [TestCase(ResolutionStatus.Agreed)]
        [TestCase(ResolutionStatus.Applied)]
        [TestCase(ResolutionStatus.Failed)]
        public void GIVEN_any_non_reversed_status_WHEN_deciding_panel_visibility_THEN_it_is_shown(ResolutionStatus status)
        {
            CouncilPanelPolicy.ShouldShow(status).Should().BeTrue();
        }

        // The status word is the only cue the player gets for where a decision stands; a mislabelled one (an agreed
        // pledge reading "Discussed", or a failed one reading "Done") misinforms them at the exact moment they are
        // reviewing what the council settled.
        [TestCase(ResolutionStatus.Proposed, "Discussed")]
        [TestCase(ResolutionStatus.Agreed, "Agreed")]
        [TestCase(ResolutionStatus.Applied, "Done")]
        [TestCase(ResolutionStatus.Failed, "Failed")]
        public void GIVEN_a_status_WHEN_labelling_it_for_the_panel_THEN_it_reads_the_expected_word(ResolutionStatus status, string expected)
        {
            CouncilPanelPolicy.StatusLabel(status).Should().Be(expected);
        }

        // The load-bearing seal rule: a resolution is remembered only once it actually COMMITTED. Sealing an
        // Agreed-but-not-yet-lifted (or a Proposed) motion would record a decision that never took effect.
        [TestCase(ResolutionStatus.Proposed)]
        [TestCase(ResolutionStatus.Agreed)]
        [TestCase(ResolutionStatus.Reversed)]
        [TestCase(ResolutionStatus.Failed)]
        public void GIVEN_a_motion_that_did_not_commit_WHEN_deciding_to_seal_THEN_it_is_not_sealed(ResolutionStatus status)
        {
            CouncilPanelPolicy.ShouldSeal(ResolutionCatalog.KeyOf(ResolutionKind.GiveGold), status).Should().BeFalse();
        }

        // An applied resolution of an ordinary kind (whose lift executor writes no member memory of its own) is the
        // whole reason the seal exists: without it the member would carry no memory that the council granted them a
        // fief, or gold, or a role, and would speak later as if it never happened.
        [Test]
        public void GIVEN_an_applied_ordinary_kind_WHEN_deciding_to_seal_THEN_it_is_sealed()
        {
            CouncilPanelPolicy.ShouldSeal(ResolutionCatalog.KeyOf(ResolutionKind.GiveGold), ResolutionStatus.Applied).Should().BeTrue();
            CouncilPanelPolicy.ShouldSeal(ResolutionCatalog.KeyOf(ResolutionKind.GrantFief), ResolutionStatus.Applied).Should().BeTrue();
        }

        // The double-memory guard: SwearOath (CommitmentBehavior.Create) and PledgeAgainst (LaunchPledge) each
        // already record the acting member their own first-person memory at the lift. Sealing again would leave the
        // NPC with two memories of the same vow, which is exactly the kind of duplicate this reconciliation removes.
        [TestCase(ResolutionKind.SwearOath)]
        [TestCase(ResolutionKind.PledgeAgainst)]
        public void GIVEN_an_applied_kind_that_already_records_its_own_memory_WHEN_deciding_to_seal_THEN_it_is_not_sealed(ResolutionKind kind)
        {
            CouncilPanelPolicy.ShouldSeal(ResolutionCatalog.KeyOf(kind), ResolutionStatus.Applied).Should().BeFalse();
        }

        // The kind is matched case-insensitively, because the key travels through the LLM's [RESOLUTION] blocks and
        // back; a casing difference must not defeat the double-memory guard and silently re-seal a pledge.
        [Test]
        public void GIVEN_a_self_memorising_kind_in_odd_casing_WHEN_deciding_to_seal_THEN_it_is_still_not_sealed()
        {
            string oddCase = ResolutionCatalog.KeyOf(ResolutionKind.SwearOath).ToUpperInvariant();

            CouncilPanelPolicy.ShouldSeal(oddCase, ResolutionStatus.Applied).Should().BeFalse();
        }
    }
}
