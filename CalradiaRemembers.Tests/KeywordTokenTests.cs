// Code written by Gabriel Mailhot, 24/06/2026.
// Documents + protects the scripted-scenarios keyword language: VERB / VERB:payload, the first-colon split,
// case + whitespace normalization, and the wildcard. The modder writes these strings, so the parse contract
// must stay exactly as shown here.

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
        [Test]
        public void GIVEN_a_bare_verb_WHEN_parsing_THEN_the_payload_is_empty_and_it_is_a_wildcard()
        {
            KeywordToken t = KeywordToken.Parse("IF_PLAYER_RULES");

            t.Verb.Should().Be("IF_PLAYER_RULES");
            t.Payload.Should().BeEmpty();
            t.Args.Should().BeEmpty();
            t.IsWildcardPayload.Should().BeTrue();
        }

        [Test]
        public void GIVEN_a_verb_with_one_argument_WHEN_parsing_THEN_verb_and_payload_split_on_the_colon()
        {
            KeywordToken t = KeywordToken.Parse("ON_DEATH:Rhagaea");

            t.Verb.Should().Be("ON_DEATH");
            t.Payload.Should().Be("Rhagaea");
            t.Args.Should().ContainSingle().Which.Should().Be("Rhagaea");
        }

        [Test]
        public void GIVEN_a_comma_payload_WHEN_parsing_THEN_args_split_on_commas_and_trim()
        {
            KeywordToken t = KeywordToken.Parse("ON_MARRIAGE: Rhagaea , Player ");

            t.Verb.Should().Be("ON_MARRIAGE");
            t.Payload.Should().Be("Rhagaea , Player");
            t.Args.Should().Equal("Rhagaea", "Player");
        }

        [Test]
        public void GIVEN_a_payload_with_further_colons_WHEN_parsing_THEN_only_the_first_colon_splits()
        {
            KeywordToken t = KeywordToken.Parse("CHANGE_RELATION:Hero:+5");

            t.Verb.Should().Be("CHANGE_RELATION");
            t.Payload.Should().Be("Hero:+5");
        }

        [Test]
        public void GIVEN_lowercase_and_padding_WHEN_parsing_THEN_the_verb_is_uppercased_and_trimmed()
        {
            KeywordToken.Parse("  on_death : Caladog ").Verb.Should().Be("ON_DEATH");
        }

        [Test]
        public void GIVEN_a_lone_star_payload_WHEN_parsing_THEN_it_is_a_wildcard()
        {
            KeywordToken.Parse("ON_DEATH:*").IsWildcardPayload.Should().BeTrue();
            KeywordToken.Parse("ON_DEATH:Rhagaea").IsWildcardPayload.Should().BeFalse();
        }

        [Test]
        public void GIVEN_blank_input_WHEN_parsing_THEN_it_yields_an_empty_verb_token_without_throwing()
        {
            KeywordToken.Parse(null).Verb.Should().BeEmpty();
            KeywordToken.Parse("   ").Verb.Should().BeEmpty();
        }

        [Test]
        public void GIVEN_two_tokens_differing_only_in_case_WHEN_compared_THEN_they_are_equal()
        {
            KeywordToken.Parse("ON_DEATH:Rhagaea")
                .Should().Be(KeywordToken.Parse("on_death:rhagaea"));
        }
    }
}
