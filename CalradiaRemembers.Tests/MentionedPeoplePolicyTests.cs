// Code written by Gabriel Mailhot, 16/11/2026.
// MentionedPeoplePolicy decides which people found in the player's message are described in the prompt, in what order,
// what is said of those left out, and how a mentioned person's present allegiance is worded. VonLemberg and his tester
// Goeth (Discord, 16/11/2026): a single shared word admitted every hero bearing it, uncapped (a block of about 6,000
// characters), and the line gave a culture but never the realm, so a model could take birth for loyalty. Too loose and
// the flood comes back; too strict and the person the player named, or the speaker's own kin, goes missing.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class MentionedPeoplePolicyTests
    {
        private static MentionCandidate HouseMatch(string key) => new MentionCandidate {Key = key, MatchedHouse = true, MatchedWords = 1};

        // STAKE: Goeth's flood: thirty heroes sharing one word of a house filled the prompt with about 6,000 characters.
        [Test]
        public void GIVEN_thirty_people_sharing_one_word_WHEN_selected_THEN_at_most_eight_are_described_and_the_rest_counted()
        {
            List<MentionCandidate> many = Enumerable.Range(0, 30).Select(i => HouseMatch("h" + i)).ToList();

            MentionSelection chosen = MentionedPeoplePolicy.Select(many);

            chosen.Shown.Should().HaveCount(MentionedPeoplePolicy.MaxShown);
            chosen.Omitted.Should().Be(30 - MentionedPeoplePolicy.MaxShown);
        }

        // STAKE: "tell me about the Battanians" names a people, not a person; describing every Battanian hero is the flood.
        [Test]
        public void GIVEN_people_found_only_by_their_culture_word_WHEN_selected_THEN_none_is_described()
        {
            var byCulture = new[] {new MentionCandidate {Key = "a", MatchedCulture = true, MatchedWords = 1}, new MentionCandidate {Key = "b", MatchedCulture = true, MatchedWords = 1}};

            MentionSelection chosen = MentionedPeoplePolicy.Select(byCulture);

            chosen.Shown.Should().BeEmpty();
            chosen.Omitted.Should().Be(0);
        }

        // STAKE: VonLemberg's regression contract: "A fully named distant hero remains resolvable even when many low-quality
        // partial matches exist." The one the player named by name must not fall past the cap.
        [Test]
        public void GIVEN_the_player_names_someone_among_many_house_matches_WHEN_selected_THEN_the_named_person_comes_first()
        {
            List<MentionCandidate> many = Enumerable.Range(0, 20).Select(i => HouseMatch("h" + i)).ToList();
            many.Add(new MentionCandidate {Key = "named", MatchedPersonalName = true, MatchedHouse = true, MatchedWords = 2});

            MentionedPeoplePolicy.Select(many).Shown.First().Should().Be("named");
        }

        // STAKE: Maifailun's "Which Debana?" family of reports: the speaker's own kin is the one meant when a name is shared.
        [Test]
        public void GIVEN_the_speakers_kin_among_namesakes_WHEN_selected_THEN_the_kin_comes_first()
        {
            var found = new[] {
                new MentionCandidate {Key = "stranger", MatchedPersonalName = true, MatchedWords = 1},
                new MentionCandidate {Key = "son", MatchedPersonalName = true, MatchedWords = 1, IsKin = true}
            };

            MentionedPeoplePolicy.Select(found).Shown.First().Should().Be("son");
        }

        // STAKE: "Known relationships ... are not lost merely because the global candidate set is large" (VonLemberg).
        [Test]
        public void GIVEN_a_known_friend_among_many_strangers_of_the_house_WHEN_selected_THEN_the_friend_is_described()
        {
            List<MentionCandidate> many = Enumerable.Range(0, 20).Select(i => HouseMatch("h" + i)).ToList();
            many.Add(new MentionCandidate {Key = "friend", MatchedHouse = true, MatchedWords = 1, IsKnown = true});

            MentionedPeoplePolicy.Select(many).Shown.Should().Contain("friend");
        }

        // STAKE: two plausible namesakes must both stay so the speaker can ask which one is meant, never invent certainty.
        [Test]
        public void GIVEN_two_namesakes_WHEN_selected_THEN_both_are_described()
        {
            var found = new[] {new MentionCandidate {Key = "a", MatchedPersonalName = true, MatchedWords = 1}, new MentionCandidate {Key = "b", MatchedPersonalName = true, MatchedWords = 1}};

            MentionedPeoplePolicy.Select(found).Shown.Should().BeEquivalentTo(new[] {"a", "b"});
        }

        // STAKE: "an honest indication if ambiguous candidates were omitted" (VonLemberg): those left out are said, so the
        // speaker asks rather than guesses; nothing is said when nobody was left out.
        [Test]
        public void GIVEN_people_left_out_WHEN_worded_THEN_the_speaker_is_told_to_ask_which()
        {
            MentionedPeoplePolicy.OmittedLine(12).Should().Contain("12 others").And.Contain("ask which");
            MentionedPeoplePolicy.OmittedLine(0).Should().BeNull();
        }

        // STAKE: Goeth saw a model read a culture as allegiance; the realm the house serves NOW is said in words.
        [Test]
        public void GIVEN_a_lord_whose_house_serves_a_realm_WHEN_worded_THEN_the_realm_is_named()
        {
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {HouseName = "dey Meroc", RealmName = "Vlandia"})
                .Should().Be("their house, dey Meroc, serves Vlandia");
        }

        // STAKE: a ruler is not said to "serve" the realm he rules.
        [Test]
        public void GIVEN_the_ruler_WHEN_worded_THEN_he_rules_the_realm()
        {
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {HouseName = "dey Vlandia", RealmName = "Vlandia", RulesTheRealm = true})
                .Should().Be("rules Vlandia");
        }

        // STAKE: "Do not fabricate a ruler or vassal oath for clans with no kingdom, mercenary companies" (VonLemberg).
        [Test]
        public void GIVEN_a_house_in_a_realms_pay_WHEN_worded_THEN_it_fights_for_pay_not_by_oath()
        {
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {HouseName = "Lochana", RealmName = "Battania", IsMercenary = true, IsFreeCompany = true})
                .Should().Be("their company, Lochana, fights for Battania for pay");
        }

        // STAKE: a house of no realm is said to be sworn to none, never given one.
        [Test]
        public void GIVEN_a_house_sworn_to_no_realm_WHEN_worded_THEN_it_is_said_so()
        {
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {HouseName = "dey Tihr"}).Should().Be("their house, dey Tihr, is sworn to no realm");
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {HouseName = "Jawwal", IsFreeCompany = true}).Should().Be("Jawwal, a free company sworn to no realm");
        }

        // STAKE: a notable or a wanderer has no house; nothing is invented, and an outlaw band has no allegiance to word.
        [Test]
        public void GIVEN_no_house_or_an_outlaw_band_WHEN_worded_THEN_nothing_is_said()
        {
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {RealmName = "Vlandia"}).Should().BeNull();
            MentionedPeoplePolicy.AllegianceClause(new AllegianceFacts {HouseName = "Sea Raiders", IsOutlawBand = true}).Should().BeNull();
        }
    }
}
