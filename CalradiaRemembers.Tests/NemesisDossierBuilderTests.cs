// Code written by Gabriel Mailhot, 20/08/2026.
// NemesisDossierBuilder turns a nemesis's raw facts (name, culture, region, times encountered, recaptures,
// extra history sentences) into the two strings the encyclopedia hero page actually renders: the "Nemesis:"
// header and the gold bullet list. The whole point of this piece is the player report (SuperGraham339):
// captured/tortured by a nemesis, escaped, killed the party, ran into him again, then could not find any
// record of him on his encyclopedia page. So the two things these tests must pin are (1) an ENCOUNTERED
// nemesis (timesEncountered > 0) shows a dossier and a LATENT one (timesEncountered == 0, never met) shows
// nothing at all (leaking a latent nemesis's existence would spoil the "hidden until materialised" design
// the whole pillar was ratified around), and (2) the wording reads naturally for one encounter/recapture vs
// several, since a raw "1 times" would look like a bug on the page.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NemesisDossierBuilderTests
    {
        // ── Encountered gate (the player report itself) ─────────────────────────

        // The exact bug the player hit: he had been captured by this nemesis, escaped, and fought him again,
        // so the dossier must show SOMETHING once timesEncountered > 0, not stay blank forever.
        [Test]
        public void GIVEN_a_nemesis_encountered_once_WHEN_building_the_label_THEN_the_header_is_shown()
        {
            NemesisDossierBuilder.BuildLabel(1).Should().Be("Nemesis:");
        }

        // Un-met nemeses must stay HIDDEN (ROADMAP-ratified design): a latent identity a capture hook has
        // not yet materialized must never surface on the encyclopedia, or the surprise/vendetta reveal beat
        // the pillar is built around gets spoiled before the player ever meets him.
        [Test]
        public void GIVEN_a_latent_nemesis_never_encountered_WHEN_building_the_label_THEN_it_is_empty_so_the_widget_autohides()
        {
            NemesisDossierBuilder.BuildLabel(0).Should().BeEmpty();
        }

        // Same gate, defensive on the items side: even if a caller mistakenly passes a full name/history for
        // an unmet nemesis, the gate must still win and hide the whole block, not just the header.
        [Test]
        public void GIVEN_a_latent_nemesis_WHEN_building_the_items_THEN_it_is_empty_even_with_other_data_supplied()
        {
            NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 0, recaptureCount: 3,
                historySentences: new List<string> { "some history line" }
            ).Should().BeEmpty();
        }

        // A nameless entry must never render: the mixin only calls this once it has resolved a real hero, but
        // this pins the fallback so a resolution bug degrades to nothing instead of a blank "leads a band of...".
        [Test]
        public void GIVEN_a_missing_display_name_WHEN_building_the_items_THEN_it_is_empty()
        {
            NemesisDossierBuilder.BuildItems(
                null, "forest bandits", "the Northmarch",
                timesEncountered: 2, recaptureCount: 0, historySentences: null
            ).Should().BeEmpty();
        }

        // ── Wording: singular vs plural ──────────────────────────────────────────

        // "1 times" reads as a bug on the page; this pins the singular phrasing at the exact boundary.
        [Test]
        public void GIVEN_exactly_one_encounter_WHEN_building_the_items_THEN_the_wording_reads_once_not_1_times()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 1, recaptureCount: 0, historySentences: null);

            items.Should().Contain("crossed his path once.");
            items.Should().NotContain("1 times");
        }

        // Several encounters must switch to the numeric plural form (the player's asked-for "times
        // encountered" fact), not stay stuck on "once".
        [Test]
        public void GIVEN_several_encounters_WHEN_building_the_items_THEN_the_wording_shows_the_count()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 4, recaptureCount: 0, historySentences: null);

            items.Should().Contain("crossed his path 4 times.");
        }

        // ── Recapture line: present only when it happened, singular vs plural ───

        // A player who has never been recaptured must not see a fabricated escape callback: the RecaptureCount
        // counter (NemesisIdentity.RecaptureCount) is the source of truth, and zero means no such line at all.
        [Test]
        public void GIVEN_zero_recaptures_WHEN_building_the_items_THEN_no_recapture_line_appears()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 2, recaptureCount: 0, historySentences: null);

            items.Should().NotContain("recaptured");
        }

        [Test]
        public void GIVEN_exactly_one_recapture_WHEN_building_the_items_THEN_the_wording_reads_once()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 2, recaptureCount: 1, historySentences: null);

            items.Should().Contain("recaptured you once");
        }

        [Test]
        public void GIVEN_several_recaptures_WHEN_building_the_items_THEN_the_wording_shows_the_count()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 5, recaptureCount: 3, historySentences: null);

            items.Should().Contain("recaptured you 3 times");
        }

        // ── Identity line: culture/region tolerant of either being missing ──────

        // The normal case: both culture and region are known, so the identity line names both (who he is,
        // per the deliverable's "who he is + culture/region" ask).
        [Test]
        public void GIVEN_culture_and_region_are_both_known_WHEN_building_the_items_THEN_both_appear_in_the_identity_line()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 1, recaptureCount: 0, historySentences: null);

            items.Should().Contain("forest bandits");
            items.Should().Contain("the Northmarch");
        }

        // RegionId can degenerate to a culture/"wilds" fallback the mixin cannot turn into a real place name
        // (NemesisBehavior.ResolveRegionId's own documented fallback); the identity line must still read as a
        // sentence, not leave a dangling "haunting the country around ." fragment.
        [Test]
        public void GIVEN_the_region_is_unresolved_WHEN_building_the_items_THEN_the_identity_line_still_reads_naturally()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", regionDisplay: null,
                timesEncountered: 1, recaptureCount: 0, historySentences: null);

            items.Should().Contain("Ranulf the Faceless Fox leads a band of forest bandits.");
            items.Should().NotContain("around .");
        }

        [Test]
        public void GIVEN_neither_culture_nor_region_is_known_WHEN_building_the_items_THEN_a_generic_identity_line_is_used()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", cultureDisplay: null, regionDisplay: null,
                timesEncountered: 1, recaptureCount: 0, historySentences: null);

            items.Should().Contain("Ranulf the Faceless Fox is a bandit boss you have crossed paths with.");
        }

        // ── History sentences: the player's literal ask ("your escape, your victories") ─────

        // The exact payoff the player report asked for: extra history (e.g. the escape/release memory
        // NemesisBehavior.RecordCaptivityEndMemory writes) must actually surface as its own bullet line.
        [Test]
        public void GIVEN_history_sentences_are_supplied_WHEN_building_the_items_THEN_each_appears_as_its_own_bullet_line()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 2, recaptureCount: 1,
                historySentences: new List<string> {
                    "I slipped his grasp and escaped his hold.",
                    "I cut him down in battle at last."
                });

            items.Should().Contain("• I slipped his grasp and escaped his hold.");
            items.Should().Contain("• I cut him down in battle at last.");
        }

        // Blank/whitespace-only entries must be skipped, or a bad upstream sentence would render an empty
        // "• " bullet on the page (visibly broken, exactly the kind of thing a player screenshots).
        [Test]
        public void GIVEN_a_blank_history_sentence_WHEN_building_the_items_THEN_it_is_skipped()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 1, recaptureCount: 0,
                historySentences: new List<string> { "  ", "", "A real history line." });

            items.Should().Contain("• A real history line.");
            items.Should().NotContainAny("•  \n", "• \n•");
        }

        // A null history list is the common case (no extra facts beyond the counters yet) and must not throw.
        [Test]
        public void GIVEN_no_history_sentences_supplied_WHEN_building_the_items_THEN_it_still_builds_the_identity_and_encounter_lines()
        {
            string items = NemesisDossierBuilder.BuildItems(
                "Ranulf the Faceless Fox", "forest bandits", "the Northmarch",
                timesEncountered: 1, recaptureCount: 0, historySentences: null);

            items.Should().Contain("• Ranulf the Faceless Fox leads a band of forest bandits, haunting the country around the Northmarch.");
            items.Should().Contain("• You have crossed his path once.");
        }
    }
}
