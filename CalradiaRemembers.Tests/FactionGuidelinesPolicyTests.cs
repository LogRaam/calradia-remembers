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

        #region a realm renamed at runtime

        // Modder report (Bellum Civile bridge, 18/09/2026). Bellum renames every kingdom while the game runs,
        // and characters went on using the vanilla name. His diagnosis: "the prompt builder seems to match a
        // fixed list and only fall back to the live name for factions it doesn't recognise" - and his evidence
        // was that REBEL kingdoms his mod spawns at runtime were named correctly, because nothing static
        // mentions them.
        //
        // He was right about the shape and there is no lookup to invert: the vanilla names are PROSE. The one
        // place a stale name is asserted about the character THEMSELVES is the heading of the block they read
        // about their own people, so that heading is now written from the live realm.

        // THE REPORTED SHAPE. The culture still matches its heading, the realm has been renamed under it, and
        // the block he reads must not go on telling him the old name.
        [Test]
        public void GIVEN_a_realm_renamed_at_runtime_WHEN_narrowed_THEN_the_heading_uses_the_live_name()
        {
            string text = Narrow("Vlandia", "Kingdom of Wercheg");

            text.Should().Contain("If you are from Kingdom of Wercheg:");
            text.Should().NotContain("If you are from Vlandia:");
        }

        // The BODY is the people, not the name: renaming a realm does not change what its lords are like, and
        // rewriting the text would be inventing a culture the file never described.
        [Test]
        public void GIVEN_a_realm_renamed_at_runtime_WHEN_narrowed_THEN_the_guidance_itself_is_untouched()
        {
            Narrow("Vlandia", "Kingdom of Wercheg")
                .Should().Contain("You believe in knightly honor and courtly love.");
        }

        // AND THE LIMIT, WRITTEN DOWN RATHER THAN WISHED AWAY. The Empire is the one culture whose headings
        // are split by REALM and not by culture, so the only thing that ever told a Southern lord from a
        // Northern one was the realm's NAME. Rename it and that distinction is gone: the loose fallback keeps
        // both imperial blocks, and re-titling both with the live name would merge two different peoples into
        // one. Two headings, one of them stale, is the lesser harm; the prompt's precedence rule carries the
        // rest. This test exists so the day someone thinks they have fixed it, they see what it would cost.
        [Test]
        public void GIVEN_an_imperial_realm_renamed_at_runtime_WHEN_narrowed_THEN_both_headings_stay_as_written()
        {
            string text = Narrow("Empire", "Basileia ton Notion Kalradon");

            text.Should().NotContain("If you are from Basileia ton Notion Kalradon:");
            text.Should().Contain("If you are from the Southern Empire:");
            text.Should().Contain("If you are from the Northern or Western Empire:");
        }

        // AN UNRENAMED REALM MUST READ EXACTLY AS IT ALWAYS DID. If this fails, every ordinary campaign just
        // had its guidelines rewritten for a bug it never had.
        [Test]
        public void GIVEN_a_realm_that_was_never_renamed_WHEN_narrowed_THEN_the_heading_is_left_alone()
        {
            Narrow("Vlandia", "Vlandia").Should().Contain("If you are from Vlandia:");
        }

        // A realm the heading ALREADY names, spelled differently by the engine, must not be re-titled into a
        // near-duplicate of itself.
        [Test]
        public void GIVEN_a_live_name_the_heading_already_carries_WHEN_narrowed_THEN_it_is_not_retitled()
        {
            Narrow("Battania", "Battania").Should().Contain("If you are from Battania:");
        }

        // No live realm to speak of (a wanderer of no kingdom): nothing to rename, and the old heading is
        // better than an empty one.
        [Test]
        public void GIVEN_a_character_with_no_realm_WHEN_narrowed_THEN_the_heading_survives_as_written()
        {
            Narrow("Vlandia", null).Should().Contain("If you are from Vlandia:");
        }

        #endregion
    
        // tashmetu (Nexus, 27/09/2026). A companion's realm is the player's, and a notable's is whoever holds the town:
        // for anyone but a lord of his own people, the realm is not his. Matched on culture PLUS realm, a Battanian
        // companion of a player serving the "Kingdom of Rhodok" read the Rhodok block as well as his own, because
        // "rhodok" is in the realm's name. His own culture decides first; the realm only settles what the culture
        // cannot (the Empire's split).
        [Test]
        public void GIVEN_a_battanian_serving_a_realm_named_for_another_people_WHEN_narrowed_THEN_only_his_own_people_are_kept()
        {
            string withRhodoks = Guidelines + "\n\nIf you are from the Rhodoks:\nYou are a pikeman of the mountain cantons.";

            string text = FactionGuidelinesPolicy.NarrowToOwnCulture(withRhodoks, "Battania", "Kingdom of Rhodok");

            text.Should().Contain("proud clan culture");
            text.Should().NotContain("pikeman of the mountain cantons");
        }

        // The realm counts as the character's own only when it is his people's: its culture is his. Otherwise the
        // caller passes no realm at all, so it can neither pull in a block nor retitle one.
        [Test]
        public void GIVEN_a_realm_of_another_culture_WHEN_asking_whose_realm_it_is_THEN_it_is_not_the_characters_own()
        {
            FactionGuidelinesPolicy.RealmOfOwnPeople("Kingdom of Prueba reino", realmCultureId: "battania", characterCultureId: "vlandia")
                                   .Should().BeNull();
            FactionGuidelinesPolicy.RealmOfOwnPeople("Kingdom of Wercheg", realmCultureId: "vlandia", characterCultureId: "vlandia")
                                   .Should().Be("Kingdom of Wercheg");
            FactionGuidelinesPolicy.RealmOfOwnPeople("Kingdom of Wercheg", realmCultureId: null, characterCultureId: "vlandia")
                                   .Should().BeNull();
        }

        // tashmetu's second case: a Vlandian notable in the player's own kingdom read "If you are from Kingdom of
        // Prueba reino:" above Vlandian text. With no realm of his own people, the heading stays his people's.
        [Test]
        public void GIVEN_a_vlandian_in_a_realm_not_of_his_people_WHEN_narrowed_THEN_the_heading_keeps_his_peoples_name()
        {
            string text = Narrow("Vlandia", FactionGuidelinesPolicy.RealmOfOwnPeople("Kingdom of Prueba reino", "battania", "vlandia"));

            text.Should().Contain("If you are from Vlandia:");
            text.Should().NotContain("Prueba reino");
        }
}
}
