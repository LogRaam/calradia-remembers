// Code written by Gabriel Mailhot, 04/07/2026.
// Documents the Chronicle's notable-intimate-conquest gate: nothing at all with adult content off or a
// nobody partner, tasteful period prose at Mature/Explicit, bawdier (still never explicit) at Hardcore.
// Chronicle entries are PERMANENT dynastic-biography text, not transient chat, so a wrong gate either
// leaks sexual content into a Chronicle for a player who set AdultContentLevel to Off (a real content
// violation, not just a bug), or writes a broken/misgendered sentence that sits on the player's page for
// the rest of the campaign. These tests pin exactly when a line forms and exactly what register it reads in.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class IntimateChroniclePolicyTests
    {
        // Stake: the hard content gate. AdultContentLevel.Off must yield ZERO chronicle text regardless of
        // scene details; a leak here would show sexual content to a player who explicitly opted out, a
        // serious content violation, not merely a wrong test.
        [Test]
        public void GIVEN_adult_content_is_off_WHEN_building_a_captive_conquest_line_THEN_nothing_is_recorded()
        {
            IntimateChroniclePolicy.BuildLine(
                    AdultContentLevel.Off, true, IntimateChronicleSceneKind.CaptiveConquest,
                    "Derthert", "Ingrid", true)
                .Should().BeNull();
        }

        // Stake: the production header is explicit ("never a random bandit-camp captive"); without the
        // partnerIsNotable gate, routine/anonymous captive scenes would spam the player's permanent
        // Chronicle biography with noise instead of reserving it for a hero of standing.
        [Test]
        public void GIVEN_the_partner_is_a_nobody_WHEN_building_a_captive_conquest_line_THEN_nothing_is_recorded()
        {
            IntimateChroniclePolicy.BuildLine(
                    AdultContentLevel.Hardcore, false, IntimateChronicleSceneKind.CaptiveConquest,
                    "Derthert", "a nameless captive", true)
                .Should().BeNull();
        }

        // Stake: fail-safe against a blank name reaching this permanent record; without it the Chronicle
        // could carry a visibly broken sentence ("...took the proud lady  to bed...") that stays on the
        // player's page for the rest of the campaign, unlike a transient chat line that scrolls away.
        [Test]
        public void GIVEN_a_missing_name_WHEN_building_a_line_THEN_nothing_is_recorded()
        {
            IntimateChroniclePolicy.BuildLine(
                    AdultContentLevel.Mature, true, IntimateChronicleSceneKind.CaptiveConquest,
                    "", "Ingrid", true)
                .Should().BeNull();
        }

        // Stake: pins the Mature-tier register (tasteful euphemism, "never explicit" per the production
        // header) plus the project-wide no-em-dash rule Gabriel greps the diff for; an em dash slipping
        // into generated prose would both violate that convention and read oddly in the Chronicle.
        [Test]
        public void GIVEN_mature_content_AND_a_notable_captive_WHEN_building_the_line_THEN_it_reads_tasteful()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.CaptiveConquest,
                "Derthert", "Ingrid", true);

            line.Should().NotBeNull();
            line.Should().Contain("Derthert").And.Contain("Ingrid").And.Contain("lady");
            line.Should().NotContain("—");
        }

        // Stake: guards that Hardcore genuinely swaps to a DIFFERENT, bawdier line rather than silently
        // reusing the Mature text, while still honoring "still never explicit" (the production header's
        // own ceiling) even at the top adult tier.
        [Test]
        public void GIVEN_hardcore_content_AND_a_notable_captive_WHEN_building_the_line_THEN_it_reads_bawdier()
        {
            string mature = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.CaptiveConquest,
                "Derthert", "Ingrid", true);
            string hardcore = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Hardcore, true, IntimateChronicleSceneKind.CaptiveConquest,
                "Derthert", "Ingrid", true);

            hardcore.Should().NotBeNull();
            hardcore.Should().NotBe(mature);
            hardcore.Should().NotContain("—");
        }

        // Stake: pins the gender-to-honorific mapping; misgendering a captive lord as "lady" in his own
        // Chronicle entry would be a directly player-visible mistake baked permanently into his biography.
        [Test]
        public void GIVEN_a_notable_male_captive_WHEN_building_the_line_THEN_the_honorific_is_lord()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.CaptiveConquest,
                "Ingrid", "Derthert", false);

            line.Should().Contain("lord").And.NotContain("lady");
        }

        // Stake: per ConceptionLine's own doc, the game's conception rule allows only one of the pair to be
        // female, so partnerIsFemale alone must correctly identify the mother; getting this backwards would
        // misattribute the pregnancy to the wrong parent in a permanent Chronicle line.
        [Test]
        public void GIVEN_the_partner_is_the_mother_WHEN_building_a_conception_line_THEN_her_name_leads_the_begetting()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.Conception,
                "Derthert", "Ingrid", true);

            line.Should().Contain("upon Ingrid").And.Contain("by Derthert");
        }

        // Stake: the mirror case of the test above, proving the phrasing tracks whichever side is actually
        // FEMALE (here, the player), not a hardcoded assumption that the partner argument is always the
        // mother; without it, a player-as-mother pregnancy would misname the father in her own Chronicle.
        [Test]
        public void GIVEN_the_player_is_the_mother_WHEN_building_a_conception_line_THEN_her_name_leads_the_begetting()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.Conception,
                "Ingrid", "Derthert", false);

            line.Should().Contain("upon Ingrid").And.Contain("by Derthert");
        }

        // Stake: confirms the "never explicit" ceiling and the no-em-dash convention hold for the
        // Conception scene kind too, not only CaptiveConquest (the two BuildLine branches must not drift
        // apart in how strictly they honor the same shared rules).
        [Test]
        public void GIVEN_hardcore_content_WHEN_building_a_conception_line_THEN_it_reads_bawdier_but_never_explicit()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Hardcore, true, IntimateChronicleSceneKind.Conception,
                "Derthert", "Ingrid", true);

            line.Should().Contain("Derthert").And.Contain("Ingrid");
            line.Should().NotContain("—");
        }
    }
}
