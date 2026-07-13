// Code written by Gabriel Mailhot, 24/06/2026.
// Documents + protects the scripted-scenarios keyword language: VERB / VERB:payload, the first-colon split,
// case + whitespace normalization, and the wildcard. The modder writes these strings, so the parse contract
// must stay exactly as shown here.
// Parsed tokens feed the trigger/guard/consequence matching in ScenarioRule and the scenario engine (see
// ROADMAP: "a keyword DSL + a composer app" is the whole moddable surface for scripted scenarios). A drift
// here either silently breaks every existing content pack's triggers, or crashes the engine on one bad line
// in a modder's own JSON.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class KeywordTokenTests
    {
        // A trigger/guard authored with no payload at all (e.g. bare "IF_PLAYER_RULES") must match ANY
        // instance of that event, not zero instances: IsWildcardPayload firing on an EMPTY payload (not
        // only an explicit "*") is what lets a modder write the simplest form of a rule.
        [Test]
        public void GIVEN_a_bare_verb_WHEN_parsing_THEN_the_payload_is_empty_and_it_is_a_wildcard()
        {
            KeywordToken t = KeywordToken.Parse("IF_PLAYER_RULES");

            t.Verb.Should().Be("IF_PLAYER_RULES");
            t.Payload.Should().BeEmpty();
            t.Args.Should().BeEmpty();
            t.IsWildcardPayload.Should().BeTrue();
        }

        // The whole DSL hinges on the FIRST colon splitting verb from payload; if this drifted, every
        // keyword a modder writes ("ON_DEATH:Rhagaea") would parse into the wrong verb or lose its
        // target entirely.
        [Test]
        public void GIVEN_a_verb_with_one_argument_WHEN_parsing_THEN_verb_and_payload_split_on_the_colon()
        {
            KeywordToken t = KeywordToken.Parse("ON_DEATH:Rhagaea");

            t.Verb.Should().Be("ON_DEATH");
            t.Payload.Should().Be("Rhagaea");
            t.Args.Should().ContainSingle().Which.Should().Be("Rhagaea");
        }

        // Multi-target keywords ("ON_MARRIAGE: Rhagaea , Player ") are modder-typed and will carry
        // stray spaces; Args must trim each one so the engine's own comparisons never fail on
        // whitespace the modder never intended as meaningful.
        [Test]
        public void GIVEN_a_comma_payload_WHEN_parsing_THEN_args_split_on_commas_and_trim()
        {
            KeywordToken t = KeywordToken.Parse("ON_MARRIAGE: Rhagaea , Player ");

            t.Verb.Should().Be("ON_MARRIAGE");
            t.Payload.Should().Be("Rhagaea , Player");
            t.Args.Should().Equal("Rhagaea", "Player");
        }

        // A payload can itself need a colon ("CHANGE_RELATION:Hero:+5", hero name, then a signed
        // delta); splitting on the FIRST colon only, never every colon, is what keeps that payload
        // intact instead of truncating it at the second colon.
        [Test]
        public void GIVEN_a_payload_with_further_colons_WHEN_parsing_THEN_only_the_first_colon_splits()
        {
            KeywordToken t = KeywordToken.Parse("CHANGE_RELATION:Hero:+5");

            t.Verb.Should().Be("CHANGE_RELATION");
            t.Payload.Should().Be("Hero:+5");
        }

        // Modders will not reliably type keywords in the exact case/whitespace the engine expects;
        // normalizing the verb here means the trigger-matcher never has to special-case a modder's
        // own formatting choices.
        [Test]
        public void GIVEN_lowercase_and_padding_WHEN_parsing_THEN_the_verb_is_uppercased_and_trimmed()
        {
            KeywordToken.Parse("  on_death : Caladog ").Verb.Should().Be("ON_DEATH");
        }

        // "*" is the EXPLICIT wildcard a modder writes to mean "any payload"; pinning it alongside the
        // bare-verb wildcard (test above) fixes both spellings of "match anything" the DSL allows, and
        // that a real payload ("Rhagaea") is NOT mistakenly treated as one.
        [Test]
        public void GIVEN_a_lone_star_payload_WHEN_parsing_THEN_it_is_a_wildcard()
        {
            KeywordToken.Parse("ON_DEATH:*").IsWildcardPayload.Should().BeTrue();
            KeywordToken.Parse("ON_DEATH:Rhagaea").IsWildcardPayload.Should().BeFalse();
        }

        // A malformed or empty keyword in a modder's content pack must degrade to a harmless
        // empty-verb token, never throw: an uncaught exception here would crash the whole scenario
        // engine over one bad line in someone else's JSON, per the Parse doc's "Never throws" contract.
        [Test]
        public void GIVEN_blank_input_WHEN_parsing_THEN_it_yields_an_empty_verb_token_without_throwing()
        {
            KeywordToken.Parse(null).Verb.Should().BeEmpty();
            KeywordToken.Parse("   ").Verb.Should().BeEmpty();
        }

        // Trigger matching compares a live CampaignEvent's keyword against every authored
        // KeywordToken; if equality were case-sensitive, a modder's own inconsistent casing between
        // two files would silently make a guard that should fire never match.
        [Test]
        public void GIVEN_two_tokens_differing_only_in_case_WHEN_compared_THEN_they_are_equal()
        {
            KeywordToken.Parse("ON_DEATH:Rhagaea")
                .Should().Be(KeywordToken.Parse("on_death:rhagaea"));
        }
    }
}
