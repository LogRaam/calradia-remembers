// Code written by Gabriel Mailhot, 09/10/2026.
// Who travelling with the player may ask for a private word, and who asks next. HakiTakiUmba (Nexus, 09/10/2026): his
// family riding in his party (siblings, a spouse) never asked for a word, only the tavern companions did. Gabriel
// (09/10/2026): the spouse already has her own channels, so balance it. If this breaks, family stays mute on the road,
// or one voice (a wife with a long shared past, say) takes every audience and the companions fall silent.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class TravellingAudiencePolicyTests
    {
        private static AudienceCandidate C(string id, int strength) => new() {HeroId = id, Strength = strength};

        // The report itself: a brother or a wife riding in the party may ask for a word, like a hired companion.
        [Test]
        public void GIVEN_family_riding_in_the_players_party_WHEN_the_roster_is_drawn_THEN_they_may_ask_for_a_word()
        {
            var brother = new TravellerFacts {IsOfPlayersClan = true, InPlayersParty = true};

            TravellingAudiencePolicy.MayAskForAWord(brother).Should().BeTrue();
            TravellingAudiencePolicy.IsKin(brother).Should().BeTrue();
        }

        // A hired companion keeps asking as before, and is not taken for family (they are of the clan too).
        [Test]
        public void GIVEN_a_hired_companion_in_the_party_WHEN_the_roster_is_drawn_THEN_they_ask_as_a_companion()
        {
            var companion = new TravellerFacts {IsHiredCompanion = true, IsOfPlayersClan = true, InPlayersParty = true};

            TravellingAudiencePolicy.MayAskForAWord(companion).Should().BeTrue();
            TravellingAudiencePolicy.IsKin(companion).Should().BeFalse();
        }

        // A private word on the road needs the person on the road: family at home writes letters instead, and a lord of
        // another house riding along is no one's to summon this way.
        [Test]
        public void GIVEN_family_away_or_a_stranger_in_the_party_WHEN_the_roster_is_drawn_THEN_they_do_not_ask()
        {
            TravellingAudiencePolicy.MayAskForAWord(new TravellerFacts {IsOfPlayersClan = true, InPlayersParty = false}).Should().BeFalse();
            TravellingAudiencePolicy.MayAskForAWord(new TravellerFacts {InPlayersParty = true}).Should().BeFalse();
            TravellingAudiencePolicy.MayAskForAWord(new TravellerFacts {IsPlayer = true, IsOfPlayersClan = true, InPlayersParty = true}).Should().BeFalse();
        }

        // The strongest matter is heard first when nobody spoke last.
        [Test]
        public void GIVEN_several_with_something_to_say_WHEN_choosing_THEN_the_strongest_matter_is_heard()
        {
            TravellingAudiencePolicy.Choose(new[] {C("wife", 40), C("companion", 70)}, null).HeroId.Should().Be("companion");
        }

        // Gabriel's balance: the wife who had the last word waits while a companion has something to say, even a lesser
        // matter, so one voice never takes every audience.
        [Test]
        public void GIVEN_the_last_audience_was_the_wifes_WHEN_a_companion_also_has_a_matter_THEN_the_companion_speaks_next()
        {
            TravellingAudiencePolicy.Choose(new[] {C("wife", 90), C("companion", 30)}, "wife").HeroId.Should().Be("companion");
        }

        // Turn-taking never silences: when only the last speaker has something to say, they still ask.
        [Test]
        public void GIVEN_only_the_last_speaker_has_a_matter_WHEN_choosing_THEN_they_still_ask()
        {
            TravellingAudiencePolicy.Choose(new[] {C("wife", 50)}, "wife").HeroId.Should().Be("wife");
            TravellingAudiencePolicy.Choose(new AudienceCandidate[0], "wife").Should().BeNull();
        }

        // Equal matters: the first in the caller's stable order, so the same person wins a tie every time.
        [Test]
        public void GIVEN_equal_matters_WHEN_choosing_THEN_the_first_listed_asks()
        {
            TravellingAudiencePolicy.Choose(new[] {C("a", 50), C("b", 50)}, null).HeroId.Should().Be("a");
        }
    }
}
