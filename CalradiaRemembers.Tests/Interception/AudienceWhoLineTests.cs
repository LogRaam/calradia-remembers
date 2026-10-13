// Code written by Gabriel Mailhot, 13/10/2026.
// What these pin: that an invitation to talk says who is asking, before the player decides whether to listen.
//
// Lordfadooboo (Nexus, 13/10/2026): "(person) wants to speak to you" when often i find myself saying "who is
// that? and dont know if i want to actually talk to that person." (fkasad seconded it.) The topic line already says
// WHAT they want (AudienceTopicLine, fkasad 18/09/2026); this line says WHO: their name with house and culture, their
// station, whether the player has met them, and how they regard the player on CR's own ledger, in the words the
// encyclopedia's "Personal regard" row uses. If it is wrong, the player declines strangers blind, or reads a regard
// that disagrees with the encyclopedia one click away.

#region

using CalradiaRemembers.Logic.Interception;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Interception
{
   [TestFixture]
   public sealed class AudienceWhoLineTests
   {
      // THE REPORTED CASE: a stranger asks for a word; the player learns who it is and that they have never met.
      [Test]
      public void GIVEN_a_stranger_asking_for_a_word_WHEN_the_invitation_is_written_THEN_it_says_who_they_are_and_that_you_never_met()
      {
         AudienceWhoLine.For(new AudienceWhoFacts {
            Label = "Luichan of Fen Company (Battanian)", Station = "a lord of Battania", HasMet = false
         }).Should().Be("\n\nWho: Luichan of Fen Company (Battanian), a lord of Battania. You have never met.");
      }

      // Someone already known: the player is reminded how they stand, in the encyclopedia's own words, so the two
      // never disagree.
      [Test]
      public void GIVEN_someone_you_have_spoken_with_WHEN_the_invitation_is_written_THEN_it_gives_their_regard_in_the_encyclopedias_words()
      {
         AudienceWhoLine.For(new AudienceWhoFacts {
            Label = "Ira of Banu Sarran (Aserai)", Station = "a merchant of Askar", HasMet = true, Regard = 12
         }).Should().Be("\n\nWho: Ira of Banu Sarran (Aserai), a merchant of Askar. You have met. Their regard for you: friendly regard.");
      }

      // The game says they met (a vanilla conversation) but CR holds no memory of them: no regard is invented.
      [Test]
      public void GIVEN_someone_met_but_never_spoken_with_through_CR_WHEN_written_THEN_no_regard_is_claimed()
      {
         AudienceWhoLine.For(new AudienceWhoFacts {Label = "Ira", Station = null, HasMet = true, Regard = null})
                        .Should().Be("\n\nWho: Ira. You have met.");
      }

      // CR keeps a memory of people the player has never stood before (a letter, a witness, word of a deed), so
      // remembering is not meeting: the line still says they never met, and gives the regard as the encyclopedia does.
      [Test]
      public void GIVEN_someone_never_met_whom_CR_remembers_WHEN_written_THEN_it_says_never_met_and_still_gives_their_regard()
      {
         AudienceWhoLine.For(new AudienceWhoFacts {Label = "Ira", HasMet = false, Regard = 0})
                        .Should().Be("\n\nWho: Ira. You have never met. Their regard for you: neutral.");
      }

      // ── Their station: who they are in the world, which is what decides whether a stranger is worth the time ──

      // A crown or a house outranks a bare "lord": the ruler and a house head are said as such.
      [Test]
      public void GIVEN_a_ruler_a_house_head_and_a_plain_lord_WHEN_described_THEN_each_reads_at_their_rank()
      {
         AudienceStation.Describe(new AudienceStationFacts {IsLord = true, IsRuler = true, IsClanLeader = true, ClanName = "dey Meroc", RealmName = "Vlandia"})
                        .Should().Be("ruler of Vlandia");
         AudienceStation.Describe(new AudienceStationFacts {IsLord = true, IsClanLeader = true, ClanName = "dey Meroc", RealmName = "Vlandia"})
                        .Should().Be("head of dey Meroc, sworn to Vlandia");
         AudienceStation.Describe(new AudienceStationFacts {IsLord = true, ClanName = "dey Meroc", RealmName = "Vlandia"})
                        .Should().Be("a lord of Vlandia");
         AudienceStation.Describe(new AudienceStationFacts {IsLord = true}).Should().Be("a lord");
      }

      // A notable is known by trade and town. The game has no display word for most notable trades (a lookup of
      // "GangLeader" prints "ERROR: Text with id ... doesn't exist!"), so the trade is read from its own name.
      [Test]
      public void GIVEN_a_notable_WHEN_described_THEN_their_trade_and_town_are_named_in_plain_words()
      {
         AudienceStation.Describe(new AudienceStationFacts {OccupationName = "Merchant", SettlementName = "Askar"})
                        .Should().Be("a merchant of Askar");
         AudienceStation.Describe(new AudienceStationFacts {OccupationName = "GangLeader", SettlementName = "Pravend"})
                        .Should().Be("a gang leader of Pravend");
         AudienceStation.Describe(new AudienceStationFacts {OccupationName = "Artisan"})
                        .Should().Be("an artisan");
      }

      // Nothing known, nothing said: the line keeps the name alone.
      [Test]
      public void GIVEN_no_station_WHEN_described_THEN_nothing_is_said()
      {
         AudienceStation.Describe(new AudienceStationFacts()).Should().BeNull();
         AudienceStation.Describe(null).Should().BeNull();
      }

      // No name, no line: an empty "Who: ." would teach the player the line is noise.
      [Test]
      public void GIVEN_no_name_WHEN_written_THEN_nothing_is_added()
      {
         AudienceWhoLine.For(new AudienceWhoFacts {Label = "  ", Station = "a lord of Battania"}).Should().BeEmpty();
         AudienceWhoLine.For(null).Should().BeEmpty();
      }
   }
}
