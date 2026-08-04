// Code written by Gabriel Mailhot, 03/08/2026.
// Documents the phrase CouncilRosterResolver attaches to each seated member so PromptBuilder's council block can
// state the truth of where they stand. STAKE (author's bug report, "bug c"): CouncilRosterPolicy seats a member
// on availability alone, by design with no distance test (see its own header), so a companion staying behind can
// be seated without ever being in the player's party; without this phrase and the directive it feeds, that
// companion could then claim in dialogue to be "already at your side" on the road. Every case here is a
// real seated-but-not-in-party shape the mod must describe honestly instead of leaving the model to guess.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class MemberPresenceDescriberTests
    {
        // The straight case a seated companion usually is: this is what keeps "at your side" TRUE when the
        // model does say it, rather than forbidding the phrase outright.
        [Test]
        public void GIVEN_a_member_riding_in_the_players_party_WHEN_describing_THEN_at_your_side_in_your_party()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(true, false, null))
                .Should().Be("at your side in your party");
        }

        // The core of bug c: a clan lord or family member who leads their own company is seated (Partie 4 rule)
        // but is NOT riding with the player. Without this phrase, nothing in the prompt contradicted a claim
        // that they were on the road together.
        [Test]
        public void GIVEN_a_member_leading_their_own_party_WHEN_describing_THEN_leading_their_own_company()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(false, true, null))
                .Should().Be("leading their own company");
        }

        // A companion or family member with no command of their own, sitting in a named town, is a second
        // shape of "seated but not travelling with you" the prompt must name honestly and specifically.
        [Test]
        public void GIVEN_a_member_staying_in_a_named_settlement_WHEN_describing_THEN_keeping_to_that_place()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(false, false, "Pravend"))
                .Should().Be("keeping to Pravend");
        }

        // A blank settlement name must not be rendered as "keeping to " with nothing after it; the fallback
        // whereabouts phrase covers it instead, exactly as it covers no settlement at all.
        [Test]
        public void GIVEN_a_member_with_a_blank_settlement_name_WHEN_describing_THEN_the_generic_abroad_phrase()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(false, false, "   "))
                .Should().Be("abroad, not travelling with you");
        }

        // The remaining seated-but-not-in-party shape: no party of their own, no known settlement (out on the
        // map, an errand, a fief visit). This is the phrase that closes the bug outright for that member.
        [Test]
        public void GIVEN_a_member_with_none_of_the_above_WHEN_describing_THEN_abroad_not_travelling_with_you()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(false, false, null))
                .Should().Be("abroad, not travelling with you");
        }

        // Priority: riding in the player's own party must win even if a (stale) fact also flags leading a
        // separate company or a settlement name, since InMainParty and LeadsOwnParty can never both be true
        // from live Hero state, and a caller must never be able to make "at your side" ambiguous.
        [Test]
        public void GIVEN_conflicting_stale_facts_WHEN_describing_THEN_in_main_party_wins()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(true, true, "Pravend"))
                .Should().Be("at your side in your party");
        }

        // Same priority guard, one rung down: leading their own company must win over a stale settlement name,
        // since a company's leader is truthfully described by their command, not by wherever it is parked.
        [Test]
        public void GIVEN_leading_their_own_party_and_a_stale_settlement_name_WHEN_describing_THEN_leading_wins()
        {
            MemberPresenceDescriber.Describe(new MemberPresenceFacts(false, true, "Pravend"))
                .Should().Be("leading their own company");
        }
    }
}
