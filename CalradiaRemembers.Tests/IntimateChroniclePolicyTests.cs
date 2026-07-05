// Code written by Gabriel Mailhot, 04/07/2026.
// Documents the Chronicle's notable-intimate-conquest gate: nothing at all with adult content off or a
// nobody partner, tasteful period prose at Mature/Explicit, bawdier (still never explicit) at Hardcore.

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
        [Test]
        public void GIVEN_adult_content_is_off_WHEN_building_a_captive_conquest_line_THEN_nothing_is_recorded()
        {
            IntimateChroniclePolicy.BuildLine(
                    AdultContentLevel.Off, true, IntimateChronicleSceneKind.CaptiveConquest,
                    "Derthert", "Ingrid", true)
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_the_partner_is_a_nobody_WHEN_building_a_captive_conquest_line_THEN_nothing_is_recorded()
        {
            IntimateChroniclePolicy.BuildLine(
                    AdultContentLevel.Hardcore, false, IntimateChronicleSceneKind.CaptiveConquest,
                    "Derthert", "a nameless captive", true)
                .Should().BeNull();
        }

        [Test]
        public void GIVEN_a_missing_name_WHEN_building_a_line_THEN_nothing_is_recorded()
        {
            IntimateChroniclePolicy.BuildLine(
                    AdultContentLevel.Mature, true, IntimateChronicleSceneKind.CaptiveConquest,
                    "", "Ingrid", true)
                .Should().BeNull();
        }

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

        [Test]
        public void GIVEN_a_notable_male_captive_WHEN_building_the_line_THEN_the_honorific_is_lord()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.CaptiveConquest,
                "Ingrid", "Derthert", false);

            line.Should().Contain("lord").And.NotContain("lady");
        }

        [Test]
        public void GIVEN_the_partner_is_the_mother_WHEN_building_a_conception_line_THEN_her_name_leads_the_begetting()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.Conception,
                "Derthert", "Ingrid", true);

            line.Should().Contain("upon Ingrid").And.Contain("by Derthert");
        }

        [Test]
        public void GIVEN_the_player_is_the_mother_WHEN_building_a_conception_line_THEN_her_name_leads_the_begetting()
        {
            string line = IntimateChroniclePolicy.BuildLine(
                AdultContentLevel.Mature, true, IntimateChronicleSceneKind.Conception,
                "Ingrid", "Derthert", false);

            line.Should().Contain("upon Ingrid").And.Contain("by Derthert");
        }

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
