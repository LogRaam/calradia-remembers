// Code written by Gabriel Mailhot, 22/10/2026.
// How far the player's name has reached a character who does not know them in person (Gabriel, 21/10/2026, after
// reading another mod's notoriety per culture: "2 et 3 m'interesse"). If this is wrong, a village notable greets a
// famous king as a stranger nobody heard of, an upstart is hailed across Calradia, a character who heard of the
// player's raid on their own land claims never to have heard the name, or the player's cruelty travels with a name
// nobody knows.

#region

using CalradiaRemembers.Logic.Social;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerFamePolicyTests
   {
      private static FameBand Band(PlayerFameFacts f) => PlayerFamePolicy.Read(f).Band;

      // STAKE: a new house with no renown is a stranger to a foreign commoner, and the character must say so rather
      // than pretend to know them.
      [Test]
      public void GIVEN_an_upstart_without_renown_WHEN_a_foreign_commoner_hears_the_name_THEN_it_is_unknown()
      {
         Band(new PlayerFameFacts {PlayerRenown = 10, Observer = FameObserver.Commoner}).Should().Be(FameBand.Unknown);
         PlayerFamePolicy.Line(PlayerFamePolicy.Read(new PlayerFameFacts {PlayerRenown = 10, Observer = FameObserver.Commoner}), "Aldric").Should().Contain("never heard the name Aldric");
      }

      // STAKE: a king of great renown is known even to a foreign commoner; greeting him as a nobody breaks the world.
      [Test]
      public void GIVEN_a_ruler_of_great_renown_WHEN_a_foreign_commoner_hears_the_name_THEN_he_is_famous()
      {
         Band(new PlayerFameFacts {PlayerRenown = 1000, PlayerRulesARealm = true, Observer = FameObserver.Commoner}).Should().Be(FameBand.Famous);
      }

      // STAKE: the halls hear of a rising house before the market does: the same renown reaches a lord further than
      // a notable, and a notable further than a wanderer.
      [Test]
      public void GIVEN_the_same_renown_WHEN_a_noble_a_notable_or_a_commoner_hears_THEN_the_noble_knows_most()
      {
         var noble = new PlayerFameFacts {PlayerRenown = 200, Observer = FameObserver.Noble};
         Band(noble).Should().Be(FameBand.ByReputation);
         Band(new PlayerFameFacts {PlayerRenown = 200, Observer = FameObserver.Notable}).Should().Be(FameBand.NameHeard);
         Band(new PlayerFameFacts {PlayerRenown = 200, Observer = FameObserver.Commoner}).Should().Be(FameBand.NameHeard);
         Band(new PlayerFameFacts {PlayerRenown = 60, Observer = FameObserver.Commoner}).Should().Be(FameBand.Unknown);
      }

      // STAKE: the player's own realm and own folk talk of their own; deeds done in a land are talked of there.
      [Test]
      public void GIVEN_a_modest_name_WHEN_heard_in_the_players_realm_culture_or_where_his_deeds_were_done_THEN_it_carries_further()
      {
         Band(new PlayerFameFacts {PlayerRenown = 60, Observer = FameObserver.Notable}).Should().Be(FameBand.NameHeard);
         Band(new PlayerFameFacts {PlayerRenown = 60, Observer = FameObserver.Notable, SameRealm = true, SameCulture = true}).Should().Be(FameBand.ByReputation);
         Band(new PlayerFameFacts {PlayerRenown = 60, Observer = FameObserver.Notable, DeedsInObserversRealm = 9}).Should().Be(FameBand.ByReputation);
         Band(new PlayerFameFacts {PlayerRenown = 60, Observer = FameObserver.Notable, DeedsInObserversRealm = 1}).Should().Be(FameBand.NameHeard);
      }

      // STAKE: fame never hides a deed: whoever heard what the player did knows them by reputation at least, so the
      // line never says "never heard the name" beside the deed it heard.
      [Test]
      public void GIVEN_a_character_who_heard_a_deed_WHEN_the_name_alone_would_be_unknown_THEN_they_know_it_by_reputation()
      {
         Band(new PlayerFameFacts {PlayerRenown = 0, Observer = FameObserver.Commoner, HeardADeed = true}).Should().Be(FameBand.ByReputation);
      }

      // STAKE: a notable of a town the player's house holds is never told his own lord's name is "faintly familiar": the
      // player can see who holds the town.
      [Test]
      public void GIVEN_a_character_who_lives_under_the_player_WHEN_the_name_alone_would_be_faint_THEN_they_know_him_by_reputation()
      {
         Band(new PlayerFameFacts {PlayerRenown = 60, Observer = FameObserver.Notable, LivesUnderThePlayer = true}).Should().Be(FameBand.ByReputation);
      }

      // STAKE: what is said of the player comes with the name: a merciless lord is said to be merciless, but only by
      // those who know the name, never by someone who has not heard it.
      [Test]
      public void GIVEN_the_players_traits_WHEN_the_name_is_known_or_not_THEN_the_repute_travels_only_with_it()
      {
         PlayerFameReading known = PlayerFamePolicy.Read(new PlayerFameFacts {PlayerRenown = 500, Observer = FameObserver.Noble, PlayerMercy = -2, PlayerHonor = 1});
         known.Repute.Should().Be(FameRepute.Merciless);
         PlayerFamePolicy.Line(known, "Aldric").Should().Contain("said to be merciless").And.Contain("Aldric");

         PlayerFamePolicy.Read(new PlayerFameFacts {PlayerRenown = 500, Observer = FameObserver.Noble, PlayerHonor = 2}).Repute.Should().Be(FameRepute.Honorable);
         PlayerFamePolicy.Read(new PlayerFameFacts {PlayerRenown = 10, Observer = FameObserver.Commoner, PlayerMercy = -2}).Repute.Should().Be(FameRepute.None);
      }
   }
}
