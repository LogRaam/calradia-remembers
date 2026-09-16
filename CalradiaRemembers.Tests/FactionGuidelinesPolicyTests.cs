// Code written by Gabriel Mailhot, 15/09/2026.
// What these pin: that a character is told about their own people and nobody else's.
//
// The FACTION BEHAVIOR section shipped all eight cultures to every NPC, as an if-then list, in every
// prompt. The cost was tokens; the damage was prose. basildingleberry (Nexus, 14/09/2026): "many of them
// seem to talk the same way" - which is what a model does when it is handed eight personality
// prescriptions at once and reads them all.
//
// The awkward cases are the ones worth pinning: the Empire splits Southern from Northern-or-Western by
// REALM rather than culture, so an Imperial with no kingdom matches no single heading and must NOT be
// stripped of his culture over it; and a total conversion's own culture matches nothing at all, where
// dropping the section is right rather than lossy.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FactionGuidelinesPolicyTests
    {
        private const string Guidelines = @"=== MEMORY AND CONSEQUENCES ===

Severe betrayal changes you.

=== FACTION BEHAVIOR ===

If you are from the Southern Empire:
You are shaped by Empress Rhagaea's progressive vision.

If you are from the Northern or Western Empire:
You hold more traditional views.

If you are from Vlandia:
You believe in knightly honor and courtly love.

If you are from Battania:
You come from a proud clan culture.

If you are from Sturgia:
You value raw strength and endurance.

If you are from the Aserai:
Your society is hierarchical and patriarchal.

If you are from the Khuzait:
You come from a steppe culture.

If you are from the Nords:
You are a seafarer of raider stock.";

        private static string Narrow(string culture, string faction)
            => FactionGuidelinesPolicy.NarrowToOwnCulture(Guidelines, culture, faction);

        // The plain case, and the whole point: one culture in, seven out.
        [Test]
        public void GIVEN_a_sturgian_WHEN_narrowed_THEN_only_the_sturgian_paragraph_survives()
        {
            string text = Narrow("Sturgia", "Sturgia");

            text.Should().Contain("You value raw strength and endurance");
            text.Should().NotContain("knightly honor");
            text.Should().NotContain("steppe culture");
            text.Should().NotContain("seafarer");
        }

        // Everything OUTSIDE the section is untouched: the narrowing must not eat a modder's other sections.
        [Test]
        public void GIVEN_any_character_WHEN_narrowed_THEN_the_other_sections_are_left_alone()
        {
            string text = Narrow("Vlandia", "Vlandia");

            text.Should().Contain("=== MEMORY AND CONSEQUENCES ===");
            text.Should().Contain("Severe betrayal changes you.");
            text.Should().Contain("=== FACTION BEHAVIOR ===");
        }

        // The Empire's split is by REALM, and the realm is what tells the two headings apart. A Southern
        // lord must not be handed the traditionalist paragraph his own court rejected.
        [Test]
        public void GIVEN_a_southern_imperial_lord_WHEN_narrowed_THEN_the_northern_paragraph_is_not_kept()
        {
            string text = Narrow("Empire", "Southern Empire");

            text.Should().Contain("Empress Rhagaea");
            text.Should().NotContain("You hold more traditional views");
        }

        // The mirror: "Northern or Western Empire" is TWO identities sharing one noun, so a Western lord
        // must match it even though the word "Western" only appears second.
        [Test]
        public void GIVEN_a_western_imperial_lord_WHEN_narrowed_THEN_the_shared_heading_still_matches_him()
        {
            string text = Narrow("Empire", "Western Empire");

            text.Should().Contain("You hold more traditional views");
            text.Should().NotContain("Empress Rhagaea");
        }

        // THE CASE THAT MUST NOT DEGRADE. An imperial wanderer belongs to no kingdom, so neither heading
        // names him outright. Narrowing to nothing would strip his culture out of the prompt entirely, so
        // the partial pass keeps what his identity touches: both imperial headings, and none of the other six.
        [Test]
        public void GIVEN_an_imperial_with_no_realm_WHEN_narrowed_THEN_both_imperial_paragraphs_are_kept_and_nothing_else()
        {
            string text = Narrow("Empire", "Unaligned");

            text.Should().Contain("Empress Rhagaea");
            text.Should().Contain("You hold more traditional views");
            text.Should().NotContain("knightly honor");
            text.Should().NotContain("proud clan culture");
        }

        // A bandit keeps the culture he was born to even when his "faction" is a band of looters.
        [Test]
        public void GIVEN_a_battanian_bandit_WHEN_narrowed_THEN_his_culture_decides_it_not_his_band()
        {
            string text = Narrow("Battania", "Looters");

            text.Should().Contain("proud clan culture");
            text.Should().NotContain("knightly honor");
        }

        // A plural heading against a singular culture name: the DLC culture is "Nord", the heading says
        // "the Nords". Written down because it is exactly the kind of match that silently fails.
        [Test]
        public void GIVEN_a_nord_WHEN_narrowed_THEN_the_plural_heading_still_matches_him()
        {
            string text = Narrow("Nord", "Nord");

            text.Should().Contain("seafarer of raider stock");
            text.Should().NotContain("steppe culture");
        }

        // A total conversion's own culture matches nothing, and that is the right answer: a Wood Elf has no
        // business being told about Vlandian knightly honour either. The heading goes with the blocks.
        [Test]
        public void GIVEN_a_culture_no_heading_names_WHEN_narrowed_THEN_the_section_is_dropped_whole()
        {
            string text = Narrow("Wood Elves", "Laurelorn");

            text.Should().NotContain("=== FACTION BEHAVIOR ===");
            text.Should().NotContain("knightly honor");
            text.Should().Contain("Severe betrayal changes you.");
        }

        // Hand-edited files must keep working: no section, nothing to narrow, nothing touched.
        [Test]
        public void GIVEN_a_text_without_the_section_WHEN_narrowed_THEN_it_is_returned_unchanged()
        {
            const string plain = "=== CONTEXT AND MOOD ===\n\nRespect the mood.";

            FactionGuidelinesPolicy.NarrowToOwnCulture(plain, "Sturgia", "Sturgia").Should().Be(plain);
        }

        // And an unknown speaker narrows nothing rather than guessing, which keeps the old behaviour
        // wherever the culture cannot be read at all.
        [Test]
        public void GIVEN_no_culture_and_no_faction_WHEN_narrowed_THEN_everything_is_kept()
        {
            string text = FactionGuidelinesPolicy.NarrowToOwnCulture(Guidelines, null, null);

            text.Should().Be(Guidelines);
        }
    }
}
