// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: qualitative rigidity words and the knownness-mirrored prompt line.
// No digits anywhere in the generated text — the encyclopedia and the prompt both speak chronicler prose.

#region

using System.Text.RegularExpressions;
using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
    [TestFixture]
    public sealed class GrudgeNarratorTests
    {
        private static Grudge NewGrudge(int rigidity, string cause = "a broken promise")
            => new Grudge
            {
                Id = "holder|3|100",
                HolderId = "holder",
                TargetId = "player",
                Source = GrudgeSource.FailedQuest,
                CauseSummary = cause,
                EventDay = 100,
                Magnitude = 50,
                Rigidity = rigidity
            };

        // ── Rigidity band words ──────────────────────────────────────────────

        [Test]
        public void GIVEN_a_very_low_rigidity_WHEN_naming_the_band_THEN_it_reads_as_a_passing_grievance()
        {
            GrudgeNarrator.RigidityWord(0).Should().Be("a passing grievance");
        }

        [Test]
        public void GIVEN_a_low_rigidity_WHEN_naming_the_band_THEN_it_reads_as_a_fresh_wound()
        {
            GrudgeNarrator.RigidityWord(GrudgeNarrator.PassingCeiling + 1).Should().Be("a fresh wound");
        }

        [Test]
        public void GIVEN_a_mid_rigidity_WHEN_naming_the_band_THEN_it_reads_as_an_old_bitterness()
        {
            GrudgeNarrator.RigidityWord(GrudgeNarrator.FreshWoundCeiling + 1).Should().Be("an old bitterness");
        }

        [Test]
        public void GIVEN_a_high_rigidity_WHEN_naming_the_band_THEN_it_reads_as_a_deep_grudge()
        {
            GrudgeNarrator.RigidityWord(GrudgeNarrator.OldBitternessCeiling + 1).Should().Be("a deep grudge");
        }

        [Test]
        public void GIVEN_maximum_rigidity_WHEN_naming_the_band_THEN_it_reads_as_unforgivable()
        {
            GrudgeNarrator.RigidityWord(GrudgePolicy.VendettaRigidity).Should().Be("unforgivable");
        }

        // ── Prompt line: the known-vs-hidden mirror ─────────────────────────

        [Test]
        public void GIVEN_a_known_grudge_WHEN_building_the_prompt_line_THEN_the_npc_may_name_it_openly()
        {
            Grudge grudge = NewGrudge(rigidity: 50);

            string line = GrudgeNarrator.PromptLine(grudge, knownToPlayer: true, effectiveMagnitude: 30);

            line.Should().Contain("openly");
            line.Should().Contain(grudge.CauseSummary);
        }

        [Test]
        public void GIVEN_a_hidden_grudge_WHEN_building_the_prompt_line_THEN_the_npc_is_instructed_to_conceal_it()
        {
            Grudge grudge = NewGrudge(rigidity: 50);

            string line = GrudgeNarrator.PromptLine(grudge, knownToPlayer: false, effectiveMagnitude: 30);

            line.Should().Contain("conceal");
            line.Should().NotContain("openly");
        }

        [Test]
        public void GIVEN_a_fully_decayed_grudge_WHEN_building_the_prompt_line_THEN_nothing_is_surfaced()
        {
            Grudge grudge = NewGrudge(rigidity: 20);

            GrudgeNarrator.PromptLine(grudge, knownToPlayer: true, effectiveMagnitude: 0).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_resolved_grudges_zero_effective_magnitude_WHEN_building_the_prompt_line_THEN_nothing_is_surfaced()
        {
            Grudge grudge = NewGrudge(rigidity: 20);

            GrudgeNarrator.PromptLine(grudge, knownToPlayer: true, effectiveMagnitude: -1).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_null_grudge_WHEN_building_the_prompt_line_THEN_it_returns_null_not_throw()
        {
            GrudgeNarrator.PromptLine(null, knownToPlayer: true, effectiveMagnitude: 50).Should().BeNull();
        }

        [Test]
        public void GIVEN_every_rigidity_band_WHEN_building_the_prompt_line_THEN_no_digits_ever_appear_in_the_generated_text()
        {
            foreach (int rigidity in new[] { 0, 10, 30, 60, 90, 100 })
            {
                Grudge grudge = NewGrudge(rigidity);

                string known = GrudgeNarrator.PromptLine(grudge, knownToPlayer: true, effectiveMagnitude: 30);
                string hidden = GrudgeNarrator.PromptLine(grudge, knownToPlayer: false, effectiveMagnitude: 30);

                Regex.IsMatch(known, @"\d").Should().BeFalse($"'{known}' must carry no digits");
                Regex.IsMatch(hidden, @"\d").Should().BeFalse($"'{hidden}' must carry no digits");
            }
        }

        [Test]
        public void GIVEN_no_cause_summary_WHEN_building_the_prompt_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
        {
            Grudge grudge = NewGrudge(rigidity: 50, cause: "");

            string line = GrudgeNarrator.PromptLine(grudge, knownToPlayer: true, effectiveMagnitude: 30);

            line.Should().NotBeNullOrWhiteSpace();
        }

        // ── Display line: the encyclopedia's third-person chronicler sentence ──

        [Test]
        public void GIVEN_the_grudge_targets_the_player_WHEN_building_the_display_line_THEN_the_player_is_named_you()
        {
            Grudge grudge = NewGrudge(rigidity: 30);

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends);

            line.Should().Contain("against you");
            line.Should().Contain(grudge.CauseSummary);
        }

        [Test]
        public void GIVEN_the_grudge_targets_a_named_npc_WHEN_building_the_display_line_THEN_the_npc_is_named_by_name()
        {
            Grudge grudge = NewGrudge(rigidity: 30);

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: false, targetName: "Derthert",
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends);

            line.Should().Contain("against Derthert");
            line.Should().NotContain("against you");
        }

        [Test]
        public void GIVEN_a_cause_that_ends_in_a_period_WHEN_building_the_display_line_THEN_the_sentence_ends_in_a_single_period()
        {
            // A player saw "...unfinished.." on the encyclopedia: causes are stored as full sentences with
            // their own punctuation, and the display line appended a second period.
            Grudge grudge = NewGrudge(rigidity: 30, cause: "The player left a task to them unfinished.");

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends);

            line.Should().EndWith("unfinished.");
            line.Should().NotContain("..");
        }

        [Test]
        public void GIVEN_a_missing_target_name_WHEN_building_the_display_line_THEN_it_falls_back_to_them_rather_than_blank()
        {
            Grudge grudge = NewGrudge(rigidity: 30);

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: false, targetName: "",
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends);

            line.Should().Contain("against them");
        }

        [Test]
        public void GIVEN_a_resolved_grudge_WHEN_building_the_display_line_THEN_nothing_is_surfaced()
        {
            Grudge grudge = NewGrudge(rigidity: 30);
            grudge.IsResolved = true;

            GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_fully_decayed_grudge_WHEN_building_the_display_line_THEN_nothing_is_surfaced()
        {
            Grudge grudge = NewGrudge(rigidity: 30);

            GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 0, resolutionKind: GrudgeResolutionKind.Amends).Should().BeNull();
        }

        [Test]
        public void GIVEN_a_null_grudge_WHEN_building_the_display_line_THEN_it_returns_null_not_throw()
        {
            GrudgeNarrator.DisplayLine(null, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends).Should().BeNull();
        }

        [Test]
        public void GIVEN_every_rigidity_band_WHEN_building_the_display_line_THEN_no_digits_ever_appear_in_the_generated_text()
        {
            foreach (int rigidity in new[] { 0, 10, 30, 60, 90, 100 })
            {
                Grudge grudge = NewGrudge(rigidity);
                grudge.ResolutionHintDiscovered = true;

                string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                    effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Deed);

                Regex.IsMatch(line, @"\d").Should().BeFalse($"'{line}' must carry no digits");
            }
        }

        [Test]
        public void GIVEN_the_resolution_hint_is_not_discovered_WHEN_building_the_display_line_THEN_no_hint_sentence_is_appended()
        {
            Grudge grudge = NewGrudge(rigidity: 30);
            grudge.ResolutionHintDiscovered = false;

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends);

            line.Should().NotContain("might");
            line.Should().NotContain("balm");
            line.Should().NotContain("mending");
        }

        [TestCase(GrudgeResolutionKind.Amends, "amend")]
        [TestCase(GrudgeResolutionKind.Gift, "gift")]
        [TestCase(GrudgeResolutionKind.Deed, "deed")]
        [TestCase(GrudgeResolutionKind.Time, "Time")]
        [TestCase(GrudgeResolutionKind.Unforgivable, "no mending")]
        public void GIVEN_the_resolution_hint_is_discovered_WHEN_building_the_display_line_THEN_the_hint_matches_the_resolution_kind(
            GrudgeResolutionKind kind, string expectedFragment)
        {
            Grudge grudge = NewGrudge(rigidity: 30);
            grudge.ResolutionHintDiscovered = true;

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: kind);

            line.Should().Contain(expectedFragment);
        }

        [Test]
        public void GIVEN_a_vendetta_grudge_with_the_hint_discovered_WHEN_building_the_display_line_THEN_it_reads_as_unforgivable_with_no_mending_path()
        {
            Grudge grudge = NewGrudge(rigidity: GrudgePolicy.VendettaRigidity);
            grudge.ResolutionHintDiscovered = true;

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: false, targetName: "Derthert",
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Unforgivable);

            line.Should().Contain("unforgivable");
            line.Should().Contain("There is no mending this.");
        }

        [Test]
        public void GIVEN_no_cause_summary_WHEN_building_the_display_line_THEN_a_fallback_phrase_is_used_instead_of_blank_text()
        {
            Grudge grudge = NewGrudge(rigidity: 30, cause: "");

            string line = GrudgeNarrator.DisplayLine(grudge, targetIsPlayer: true, targetName: null,
                effectiveMagnitude: 20, resolutionKind: GrudgeResolutionKind.Amends);

            line.Should().Contain("an old matter");
        }
    }
}
