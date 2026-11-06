// Code written by Gabriel Mailhot, 05/11/2026.
// What a character on the campaign map is told of the forces in sight. 3chmidt (Nexus, 05/11/2026): a friendly lord did
// not notice "an enemy army with 1800 soldiers" five meters away, and acted surprised when it was mentioned. If the
// choice is wrong, the army at the door goes unseen while a far band of looters is named, the count reads as an exact
// tally nobody could make, or a friend is called a foe.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.War;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ForcesInSightTests
    {
        // STAKE: 3chmidt's case. The enemy army beside the conversation is named, with its banner, its leader and its size.
        [Test]
        public void GIVEN_an_enemy_army_of_1800_close_by_WHEN_told_THEN_the_character_sees_it_as_an_enemy_army_of_some_1800()
        {
            List<string> lines = ForcesInSight.Lines(new[] {
                new SightedForce {Leader = "Lord Derthert", Banner = "Vlandia", Men = 1812, IsArmy = true, Side = SightedSide.Enemy, Distance = 1f}
            });

            lines.Should().Equal("an enemy army of Vlandia under Lord Derthert, some 1,800 men");
        }

        // STAKE: the nearest forces are the ones that matter to a conversation; a far one is left out before a near one.
        [Test]
        public void GIVEN_more_forces_than_can_be_named_WHEN_told_THEN_the_nearest_are_named_first()
        {
            var sighted = new List<SightedForce>();
            for (var i = 0; i < 8; i++)
                sighted.Add(new SightedForce {Banner = "looters", Men = 10 + i, Side = SightedSide.Outlaws, Distance = 8 - i});
            sighted.Add(new SightedForce {Leader = "Lady Ira", Banner = "the Empire", Men = 300, IsArmy = true, Side = SightedSide.Friendly, Distance = 0.5f});

            List<string> lines = ForcesInSight.Lines(sighted);

            lines.Should().HaveCount(ForcesInSight.MaxNamed);
            lines[0].Should().Be("a friendly army of the Empire under Lady Ira, some 300 men");
        }

        // STAKE: an army and a lone party side by side: the army, the greater threat or shield, is named first.
        [Test]
        public void GIVEN_an_army_and_a_party_at_the_same_distance_WHEN_told_THEN_the_army_comes_first()
        {
            List<string> lines = ForcesInSight.Lines(new[] {
                new SightedForce {Leader = "Lord A", Banner = "Sturgia", Men = 80, Side = SightedSide.Neutral, Distance = 2f},
                new SightedForce {Leader = "Lord B", Banner = "Sturgia", Men = 900, IsArmy = true, Side = SightedSide.Neutral, Distance = 2f}
            });

            lines[0].Should().Be("an army of Sturgia under Lord B, neither friend nor foe, some 900 men");
            lines[1].Should().Be("a party of Sturgia under Lord A, neither friend nor foe, some 80 men");
        }

        // STAKE: nobody counts a column at a glance: a handful stays a handful, and large hosts read in round figures.
        [Test]
        public void GIVEN_forces_of_every_size_WHEN_counted_THEN_they_read_as_a_captain_judges_them()
        {
            ForcesInSight.MenWords(7).Should().Be("a handful of men");
            ForcesInSight.MenWords(43).Should().Be("some 40 men");
            ForcesInSight.MenWords(268).Should().Be("some 250 men");
            ForcesInSight.MenWords(1812).Should().Be("some 1,800 men");
        }

        // STAKE: nothing in sight, nothing said: the character is not handed an empty heading to fill with invention.
        [Test]
        public void GIVEN_nothing_in_sight_WHEN_told_THEN_there_is_nothing_to_say()
        {
            ForcesInSight.Lines(new SightedForce[0]).Should().BeEmpty();
            ForcesInSight.Lines(new[] {new SightedForce {Leader = "Lord Empty", Men = 0, Distance = 1f}}).Should().BeEmpty();
        }

        // STAKE: the forces told are the player's surroundings; a lord met on the road, or riding with him, sees them too.
        [Test]
        public void GIVEN_a_speaker_beside_the_player_WHEN_asked_whether_he_sees_THEN_he_does()
        {
            ForcesInSight.SpeakerSeesWhatThePlayerSees(true, null).Should().BeTrue();
            ForcesInSight.SpeakerSeesWhatThePlayerSees(false, 1f).Should().BeTrue();
        }

        // STAKE: a speaker elsewhere (a letter, a far lord) would be told of armies he cannot see: he is told none.
        [Test]
        public void GIVEN_a_speaker_away_from_the_player_WHEN_asked_whether_he_sees_THEN_he_does_not()
        {
            ForcesInSight.SpeakerSeesWhatThePlayerSees(false, ForcesInSight.FaceToFaceWithin + 20f).Should().BeFalse();
            ForcesInSight.SpeakerSeesWhatThePlayerSees(false, null).Should().BeFalse();
        }

        // STAKE: outlaws fly no realm's banner: they are a band, never "an enemy army of" anyone.
        [Test]
        public void GIVEN_sea_raiders_in_sight_WHEN_told_THEN_they_read_as_a_band()
        {
            ForcesInSight.Describe(new SightedForce {Banner = "sea raiders", Men = 34, Side = SightedSide.Outlaws})
                         .Should().Be("a band of sea raiders, some 30 men");
        }
    }
}
