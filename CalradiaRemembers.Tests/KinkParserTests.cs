// Code written by Gabriel Mailhot, 01/08/2026.
// Guards the mapping from author-written kink names (character_overrides.json "kinks" field) to Kink
// values. Player request: kinks added with cr.add_kink were wiped on cr.reload_lore (RederiveKinks
// re-derives from current traits every reload); authoring them in the override file instead locks them so
// they survive a reload and let the player stage a scenario. The parsed list feeds
// CharacterOverride.ParsedKinks, which CharacterLoreBehavior and the per-turn chat sync both apply onto
// NpcProfile.Romantic.Kinks, REPLACING whatever was rolled/frozen. A name that fails to parse must be
// skipped quietly, never throw: a typo in a hand-edited file must never crash the campaign.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class KinkParserTests
    {
        // A player types the enum's own member names (as listed by cr.kinks) into the JSON file, and the
        // parser has to accept them exactly, whatever the case, since that is the only vocabulary offered.
        [TestCase("Dominance", Kink.Dominance)]
        [TestCase("dominance", Kink.Dominance)]
        [TestCase("DOMINANCE", Kink.Dominance)]
        [TestCase("Praise", Kink.Praise)]
        [TestCase("SwitchTendencies", Kink.SwitchTendencies)]
        [TestCase("switchtendencies", Kink.SwitchTendencies)]
        public void GIVEN_a_known_kink_name_WHEN_parsing_THEN_it_maps_to_that_kink(string raw, Kink expected)
        {
            KinkParser.Parse(new[] {raw}).Should().ContainSingle().Which.Should().Be(expected);
        }

        // The stake this test guards: a typo ("Dominence") or a name from an older/renamed enum value must
        // not blow up the whole authored-override load (CharacterOverrideLoader wraps the file in a
        // try/catch, but a per-name throw here would still surface as a parse failure for the WHOLE file,
        // not just the one bad kink). Unknown names are silently dropped instead.
        [Test]
        public void GIVEN_a_mix_of_valid_and_unknown_names_WHEN_parsing_THEN_only_the_valid_ones_survive()
        {
            List<Kink> result = KinkParser.Parse(new[] {"Dominance", "Nonsense", "Praise", "Dominence"});

            result.Should().Equal(Kink.Dominance, Kink.Praise);
        }

        // A player who copies a kink twice into the JSON array (or re-adds one already locked) must not end
        // up with a duplicated entry reaching NpcProfile.Romantic.Kinks: cr.kinks would then show the same
        // kink twice, and Kinks.Contains checks elsewhere assume a de-duplicated list.
        [Test]
        public void GIVEN_a_duplicated_name_WHEN_parsing_THEN_only_the_first_occurrence_is_kept()
        {
            List<Kink> result = KinkParser.Parse(new[] {"Praise", "Dominance", "praise"});

            result.Should().Equal(Kink.Praise, Kink.Dominance);
        }

        // Blank/whitespace entries are the kind of thing a trailing comma or a stray empty string in a
        // hand-edited JSON array produces; they must be ignored, not treated as an unknown-name skip that
        // still counts toward anything.
        [Test]
        public void GIVEN_blank_or_whitespace_entries_WHEN_parsing_THEN_they_are_ignored()
        {
            List<Kink> result = KinkParser.Parse(new[] {"", "   ", "Praise"});

            result.Should().Equal(Kink.Praise);
        }

        // CharacterOverride.ParsedKinks is designed to distinguish "field omitted" (null) from "field present
        // but empty" (locks to NO kinks); that distinction lives in CharacterOverride, not here, but Parse
        // itself must still behave predictably on null/empty input: an empty list, never null, so a caller
        // that already decided to call Parse never needs a second null-check on the result.
        [Test]
        public void GIVEN_null_names_WHEN_parsing_THEN_it_returns_an_empty_list()
        {
            KinkParser.Parse(null).Should().BeEmpty();
        }

        [Test]
        public void GIVEN_an_empty_collection_WHEN_parsing_THEN_it_returns_an_empty_list()
        {
            KinkParser.Parse(new string[0]).Should().BeEmpty();
        }

        // ── Unrecognized names (forgiving parse, but never silent) ──

        // Parsing stays forgiving so a typo cannot crash a campaign, but the author must be able to LEARN of the
        // typo: an authored lock of nothing but misspellings quietly pins the NPC to zero kinks, indistinguishable
        // from a deliberate empty lock. Unrecognized reports exactly the names that were dropped, trimmed.
        [Test]
        public void GIVEN_typos_among_valid_names_WHEN_listing_unrecognized_THEN_only_the_typos_are_reported()
        {
            KinkParser.Unrecognized(new[] {"Praise", " Dominence ", "NotAKink"})
                .Should().Equal("Dominence", "NotAKink");
        }

        // A clean list reports nothing, so a warning shown to the player always means a real problem.
        [Test]
        public void GIVEN_only_valid_names_WHEN_listing_unrecognized_THEN_none_are_reported()
        {
            KinkParser.Unrecognized(new[] {"Praise", "dominance"}).Should().BeEmpty();
        }

        // Blank entries are formatting noise, not typos: reporting them would train the author to ignore warnings.
        // Null input yields an empty list, never null, mirroring Parse.
        [Test]
        public void GIVEN_blank_or_null_input_WHEN_listing_unrecognized_THEN_nothing_is_reported()
        {
            KinkParser.Unrecognized(new[] {"", "   "}).Should().BeEmpty();
            KinkParser.Unrecognized(null).Should().BeEmpty();
        }
    }
}
