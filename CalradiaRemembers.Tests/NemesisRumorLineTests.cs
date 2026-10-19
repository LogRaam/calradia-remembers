// Code written by Gabriel Mailhot, 21/08/2026.
// NemesisRumorLine composes the tavern-keeper's nemesis rumour reply: the last open bit of the
// nemesis-discoverability pillar (ROADMAP: "a standalone tavern-keeper rumour variant remains an optional
// minor follow-up" to the dossier + NemesisBounty interception trigger shipped in v2.1.2). The design
// constraint that matters most here: this is DISCOVERABILITY ONLY. It names the nemesis and his region and
// points the player at the existing bounty channel, but must never itself state a reward or imply the
// bounty is already settled, since NemesisBountyDialogFlow alone owns issuing/settling it (no duplication
// of that machinery). These tests pin the wording so a future edit cannot accidentally leak bounty
// specifics into this line.

#region

using System.Linq;
using CalradiaRemembers.Logic.Nemesis;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisRumorLineTests
    {
        // The normal case: both the nemesis and his region are known, so the line must name both, exactly
        // as the tavern-keeper's spoken reply is meant to (the discoverability ask itself).
        [Test]
        public void GIVEN_a_nemesis_label_and_region_WHEN_composing_THEN_both_appear_in_the_line()
        {
            string line = NemesisRumorLine.Compose("Coll the Unbroken", "Vlandia");

            line.Should().Contain("Coll the Unbroken");
            line.Should().Contain("Vlandia");
        }

        // The line must point at the EXISTING bounty channel without stating a reward or any settled state
        // itself: NemesisBountyDialogFlow alone owns issuing/settling a bounty, so a gold figure leaking in
        // here would duplicate that machinery instead of just gesturing at it ("some lord hereabouts would
        // pay well to see him gone").
        [Test]
        public void GIVEN_a_composed_line_WHEN_inspecting_it_THEN_it_never_states_a_reward_figure()
        {
            string line = NemesisRumorLine.Compose("Coll the Unbroken", "Vlandia");

            line.Should().NotContainAny("denars", "gold");
            line.Any(char.IsDigit).Should().BeFalse();
        }

        // RegionId can degenerate to an unresolved fallback (mirrors NemesisDossierFactory.ResolveRegionDisplay's
        // own documented tolerance); the line must still read as a sentence, not leave a dangling
        // "raiding near ..." fragment.
        [Test]
        public void GIVEN_no_region_is_known_WHEN_composing_THEN_it_falls_back_to_a_generic_region_phrase()
        {
            string line = NemesisRumorLine.Compose("Coll the Unbroken", null);

            line.Should().Contain("Coll the Unbroken has been raiding near these lands");
        }

        // A missing label means the caller's own eligibility gate (an encountered, region-matched nemesis)
        // was bypassed somehow; this must degrade to nothing rather than speak a nameless rumour.
        [Test]
        public void GIVEN_no_nemesis_label_WHEN_composing_THEN_the_line_is_empty()
        {
            NemesisRumorLine.Compose(null, "Vlandia").Should().BeEmpty();
            NemesisRumorLine.Compose("   ", "Vlandia").Should().BeEmpty();
        }
    }
}
