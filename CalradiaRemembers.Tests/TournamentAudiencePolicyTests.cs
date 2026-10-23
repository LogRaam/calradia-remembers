// Code written by Gabriel Mailhot, 23/10/2026.
// TournamentAudiencePolicy decides who in the town keeps a spectator's memory of a tournament the player fought in.
// chmidt (Nexus, 23/10/2026): the lady of Hargard "couldn't even remember that I won a tournament at her city". If this is
// wrong, the house that holds the town forgets the tourney held under its own walls, a crowded town floods every
// stranger's memory, or a man who fought in it gets a bystander's line beside his own.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Tournaments;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TournamentAudiencePolicyTests
   {
      private static TournamentResult Won() => new("Hargard", 100, true, 0, null, new List<TournamentBout>());

      // STAKE: chmidt's lady of Hargard: the holding house's lords and ladies in town remember, even never spoken to.
      [Test]
      public void GIVEN_a_lady_of_the_holding_house_in_town_WHEN_the_audience_is_chosen_THEN_she_watched()
      {
         TournamentAudiencePolicy.Who(new[] {new TournamentOnlooker {Id = "lady", IsLord = true, OfTheHoldingHouse = true}})
                                 .Should().Equal("lady");
      }

      // STAKE: a stranger in town is not written into; a known lord or notable is.
      [Test]
      public void GIVEN_strangers_and_acquaintances_WHEN_the_audience_is_chosen_THEN_only_the_known_and_the_holding_house_watch()
      {
         TournamentAudiencePolicy.Who(new[] {
            new TournamentOnlooker {Id = "stranger", IsLord = true},
            new TournamentOnlooker {Id = "notable", KnownToThePlayer = true},
            new TournamentOnlooker {Id = "known_lord", IsLord = true, KnownToThePlayer = true},
            new TournamentOnlooker {Id = "unknown_notable"}
         }).Should().BeEquivalentTo("notable", "known_lord");
      }

      // STAKE: a hero who fought in it keeps the fighter's memory, not a second spectator's one.
      [Test]
      public void GIVEN_an_onlooker_who_fought_in_it_WHEN_the_audience_is_chosen_THEN_he_is_left_out()
      {
         TournamentAudiencePolicy.Who(new[] {new TournamentOnlooker {Id = "fighter", IsLord = true, OfTheHoldingHouse = true, FoughtInIt = true}})
                                 .Should().BeEmpty();
      }

      // STAKE: a crowded town keeps a few memories, the holding house first.
      [Test]
      public void GIVEN_a_crowded_town_WHEN_the_audience_is_chosen_THEN_it_is_capped_with_the_holding_house_first()
      {
         var many = new List<TournamentOnlooker>();
         for (int i = 0; i < 12; i++) many.Add(new TournamentOnlooker {Id = "known" + i, KnownToThePlayer = true});
         many.Add(new TournamentOnlooker {Id = "lady", IsLord = true, OfTheHoldingHouse = true});

         IReadOnlyList<string> who = TournamentAudiencePolicy.Who(many);

         who.Should().HaveCount(TournamentAudiencePolicy.MaxOnlookers);
         who[0].Should().Be("lady");
      }

      // STAKE: the memory says what the onlooker saw: the player crowned, or where the player fell.
      [Test]
      public void GIVEN_the_result_WHEN_the_spectators_memory_is_written_THEN_it_tells_the_players_fate()
      {
         TournamentAudiencePolicy.SpectatorMemory(Won()).Should().Be("I watched the tourney at Hargard, where the player took the crown.");
         TournamentAudiencePolicy.SpectatorMemory(new TournamentResult("Hargard", 100, false, 3, "x", new List<TournamentBout>()))
                                 .Should().Be("I watched the tourney at Hargard, where the player fell in the semi-final.");
      }
   }
}
