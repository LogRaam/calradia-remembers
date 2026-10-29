// Code written by Gabriel Mailhot, 29/10/2026.
// Who a person in the room is to the one speaking: kin, and the rank they hold. The words land on that person's line in
// the speaker's prompt. fearente (Nexus, 29/10/2026): a lady governor spoke to her father, the head of her clan, "like
// he was a servant", and a ruler was treated like an ordinary noble; he asked that characters know who the others
// present are, "their clan, rulers and superiors". If these words are wrong or missing, a daughter orders her father
// about, a lord addresses his own king as a peer, and a mother is voiced as a stranger.

#region

using CalradiaRemembers.Logic.Witnesses;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Witnesses
{
    [TestFixture]
    public sealed class WitnessStandingTextTests
    {
        // The report itself: her father, who leads her clan, read as "your clan member". Both facts must reach her.
        [Test]
        public void GIVEN_a_father_who_heads_the_speakers_clan_WHEN_described_THEN_she_is_told_he_is_her_father_and_her_clan_head()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {Kin = WitnessKin.Parent, HeadsClan = "Pethros", HeadsSpeakersClan = true})
                               .Should().Be("your father, head of your clan");
        }

        // A foreign ruler read as "a lord you know": he must be named as the sovereign of his realm.
        [Test]
        public void GIVEN_the_ruler_of_another_realm_WHEN_described_THEN_he_is_named_its_ruler()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {RulesRealm = "Vlandia", HeadsClan = "dey Meroc"})
                               .Should().Be("ruler of Vlandia");
        }

        // The speaker's own sovereign in the room is not just any ruler: the speaker owes them fealty.
        [Test]
        public void GIVEN_the_speakers_own_ruler_WHEN_described_THEN_they_are_named_the_speakers_sovereign()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {RulesRealm = "the Southern Empire", RulesSpeakersRealm = true, IsFemale = true})
                               .Should().Be("ruler of the Southern Empire and your sovereign");
        }

        // A mother who is also the queen: both the kinship and the crown, kin first since it is how a child speaks to her.
        [Test]
        public void GIVEN_a_mother_who_rules_the_realm_and_the_clan_WHEN_described_THEN_kin_comes_first_then_her_crown_and_her_clan()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {
                Kin = WitnessKin.Parent, IsFemale = true, RulesRealm = "the Southern Empire", RulesSpeakersRealm = true,
                HeadsClan = "Pethros", HeadsSpeakersClan = true
            }).Should().Be("your mother, ruler of the Southern Empire and your sovereign, head of your clan");
        }

        // A lord who leads another house is the speaker's peer at best: he is named with his house.
        [Test]
        public void GIVEN_the_head_of_another_clan_WHEN_described_THEN_his_clan_is_named()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {HeadsClan = "Comnos"}).Should().Be("head of the Comnos clan");
        }

        // fearente's lady governor: the post she holds is part of who she is in the room.
        [Test]
        public void GIVEN_a_sister_who_governs_a_town_WHEN_described_THEN_both_are_said()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {Kin = WitnessKin.Sibling, IsFemale = true, Governs = "Pravend"})
                               .Should().Be("your sister, governor of Pravend");
        }

        // Kin words are gendered on the person present, never guessed from a name (the 2026-08-31 report on witnesses).
        [TestCase(WitnessKin.Spouse, false, "your husband")]
        [TestCase(WitnessKin.Spouse, true, "your wife")]
        [TestCase(WitnessKin.Child, false, "your son")]
        [TestCase(WitnessKin.Child, true, "your daughter")]
        [TestCase(WitnessKin.Sibling, false, "your brother")]
        [TestCase(WitnessKin.Grandparent, true, "your grandmother")]
        [TestCase(WitnessKin.Grandchild, false, "your grandson")]
        public void GIVEN_close_kin_WHEN_described_THEN_the_word_matches_their_sex(WitnessKin kin, bool female, string expected)
        {
            WitnessStandingText.Describe(new WitnessStandingFacts {Kin = kin, IsFemale = female}).Should().Be(expected);
        }

        // The council writes every seat at once, so a seat's kin is said of the seat: her father's line names her.
        [Test]
        public void GIVEN_a_father_seated_with_his_daughter_and_the_player_his_son_in_law_WHEN_his_seat_is_written_THEN_both_ties_are_said()
        {
            WitnessStandingText.AtTable(new[] {
                new TableTie {Kin = WitnessKin.Parent, Other = "Lady Ira"}, new TableTie {Kin = WitnessKin.Spouse, OtherIsPlayer = true}
            }, false).Should().Be("father of Lady Ira; the player's husband");
        }

        // A seat kin to no one present gets no clause at all.
        [Test]
        public void GIVEN_a_seat_kin_to_no_one_WHEN_written_THEN_there_is_no_clause()
        {
            WitnessStandingText.AtTable(new[] {new TableTie {Kin = WitnessKin.None, Other = "Lord Olek"}}, false).Should().BeNull();
            WitnessStandingText.AtTable(null, true).Should().BeNull();
        }

        // Nothing to say must stay nothing: an ordinary witness keeps exactly the line it had before.
        [Test]
        public void GIVEN_no_kin_and_no_rank_WHEN_described_THEN_there_is_no_clause()
        {
            WitnessStandingText.Describe(new WitnessStandingFacts()).Should().BeNull();
            WitnessStandingText.Describe(null).Should().BeNull();
        }
    }
}
