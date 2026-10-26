// Code written by Gabriel Mailhot, 26/10/2026.
// HouseHeadMarriagePolicy names the game's bar on wedding two heads of houses before the model is told a marriage is
// on offer. RaveN017Mod (Nexus, 26/10/2026) courted Rhagaea, who heads her house, was told they were wed, and nothing
// was sealed: the game had refused in silence. If this is wrong, either a ruler "marries" the player in words again
// with nothing behind it, or a lady who heads no house is barred from a match the game would honour.

#region

using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class HouseHeadMarriagePolicyTests
   {
      // STAKE: RaveN017Mod's Rhagaea. Two heads of houses: the game seals nothing, so the match must be barred and named.
      [Test]
      public void GIVEN_she_heads_her_house_as_the_player_heads_theirs_WHEN_asked_THEN_the_match_is_barred()
      {
         HouseHeadMarriagePolicy.BarsTheMatch(new HouseHeadFacts {CandidateHeadsTheirHouse = true, PlayerHeadsTheirHouse = true})
            .Should().BeTrue();
      }

      // STAKE: a daughter or sister of a house (Ira, say) is the ordinary match; barring her would shut the whole feature.
      [Test]
      public void GIVEN_she_heads_no_house_WHEN_asked_THEN_the_match_is_not_barred()
      {
         HouseHeadMarriagePolicy.BarsTheMatch(new HouseHeadFacts {CandidateHeadsTheirHouse = false, PlayerHeadsTheirHouse = true})
            .Should().BeFalse();
      }

      // STAKE: the game's bar needs BOTH heads; a player who has handed their house on must not be refused for hers alone.
      [Test]
      public void GIVEN_the_player_heads_no_house_WHEN_asked_THEN_her_house_alone_does_not_bar_it()
      {
         HouseHeadMarriagePolicy.BarsTheMatch(new HouseHeadFacts {CandidateHeadsTheirHouse = true, PlayerHeadsTheirHouse = false})
            .Should().BeFalse();
      }
   }
}
