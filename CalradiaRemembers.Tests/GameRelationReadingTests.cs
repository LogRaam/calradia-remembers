// Code written by Gabriel Mailhot, 04/10/2026.
// What the game's relation with the player means for one character: their clan's standing, and, when a mod gives
// them one, a relation of their own. tashmetu (Nexus, 04/10/2026), author of Personal Relations (each noble gets a
// relation of their own with the player instead of sharing their clan leader's): the prompt's "Your clan's standing"
// was read from Hero.GetRelationWithPlayer(), which in the base game IS the leader's relation, but with that mod is
// the character's own. Players saw a lord at +100 "clan standing" and 0 personal regard, and the model spoke as if
// the house loved them while the lord did not know them. The clan's standing is the leader's; the character's own
// is said apart, and only when it truly differs, so a game without such a mod sees exactly what it saw before.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class GameRelationReadingTests
   {
      // tashmetu's case: the daughter's own relation is 0 while her father, the clan's leader, is at +100. The clan's
      // standing is her father's; her own is said apart.
      [Test]
      public void GIVEN_a_clan_member_whose_own_relation_differs_from_her_leaders_WHEN_read_THEN_the_clan_standing_is_the_leaders_and_hers_is_said_apart()
      {
         var f = new GameRelationFacts {OwnRelation = 0, LeaderRelation = 100};

         GameRelationReading.ClanStanding(f).Should().Be(100);
         GameRelationReading.OwnRelationIfDistinct(f).Should().Be(0);
      }

      // The base game: a clan member shares the leader's relation. Nothing extra is said, so the prompt of every player
      // without such a mod is unchanged.
      [Test]
      public void GIVEN_the_base_game_where_a_clan_shares_its_leaders_relation_WHEN_read_THEN_nothing_more_is_said()
      {
         var f = new GameRelationFacts {OwnRelation = 40, LeaderRelation = 40};

         GameRelationReading.ClanStanding(f).Should().Be(40);
         GameRelationReading.OwnRelationIfDistinct(f).Should().BeNull();
      }

      // The clan's leader, a hero of no clan, or a member of the player's own clan (whose leader is the player): no
      // leader's relation to read apart, so the character's own relation is the standing, said once.
      [Test]
      public void GIVEN_no_other_leader_to_read_WHEN_read_THEN_the_characters_own_relation_is_the_standing()
      {
         var f = new GameRelationFacts {OwnRelation = -12, LeaderRelation = null};

         GameRelationReading.ClanStanding(f).Should().Be(-12);
         GameRelationReading.OwnRelationIfDistinct(f).Should().BeNull();
      }
   }
}
