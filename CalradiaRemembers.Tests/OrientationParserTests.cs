// Code written by Gabriel Mailhot, 10/07/2026.
// Guards the mapping from a hand-typed orientation word (character_overrides.json or the cr.orientation
// console command) to a SexualOrientation. Player request: let a player override an NPC's rolled orientation,
// e.g. to enable a female-female romance the roll had blocked (v1.30.0 CHANGELOG). The parsed value feeds
// CharacterOverride.ParsedOrientation, synced onto the NPC's RomanticProfile before every prompt, and the
// jealousy system's plausibility check reads it too (a same-sex pair is only plausible courtship once the
// orientation allows it). A word that fails to parse must return null quietly, never throw: a typo in a
// hand-edited file must never crash the campaign.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OrientationParserTests
    {
        // This is what a player actually types by hand, in JSON or the cr.orientation console command: casual
        // words and synonyms ("gay" and "lesbian" both meaning Homosexual, "bi" short for "bisexual"), not just
        // the enum's own names. Missing a synonym is the exact failure a player would hit when they try to
        // enable the romance they wanted and nothing changes, with no error to tell them why.
        [TestCase("straight", SexualOrientation.Heterosexual)]
        [TestCase("Hetero", SexualOrientation.Heterosexual)]
        [TestCase("bi", SexualOrientation.Bisexual)]
        [TestCase("BISEXUAL", SexualOrientation.Bisexual)]
        [TestCase("gay", SexualOrientation.Homosexual)]
        [TestCase("lesbian", SexualOrientation.Homosexual)]
        [TestCase("pan", SexualOrientation.Pansexual)]
        [TestCase("asexual", SexualOrientation.Asexual)]
        [TestCase("bi-curious", SexualOrientation.BiCurious)]
        public void GIVEN_a_known_word_WHEN_parsing_THEN_it_maps_to_the_orientation(string raw, SexualOrientation expected)
        {
            OrientationParser.Parse(raw).Should().Be(expected);
        }

        // A parse failure has to fail SAFE: null, never an exception. CharacterOverrideLoader.Load already
        // wraps the whole file in a try/catch so a bad JSON structure can't crash the campaign, but a value
        // that parses fine as a string and merely isn't a recognized word (an unset field, a typo, a joke
        // entry) must degrade to "no override" just as quietly, leaving the NPC's rolled orientation untouched.
        [TestCase("")]
        [TestCase(null)]
        [TestCase("attack helicopter")]
        public void GIVEN_an_unknown_or_empty_word_WHEN_parsing_THEN_it_returns_null(string raw)
        {
            OrientationParser.Parse(raw).Should().BeNull();
        }
    }
}
