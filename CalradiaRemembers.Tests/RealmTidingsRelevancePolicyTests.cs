// Code written by Gabriel Mailhot, 27/10/2026.
// RealmTidingsRelevancePolicy decides who writes the player news of the realm unbidden, and which news is worth the letter.
// DepaHN (Nexus, 26/10/2026): a northern lord he had never met kept writing him of northern wars that did not touch him.
// If this is wrong, strangers fill the mailbox with distant bulletins again, or a friend falls silent about the very
// things the player cares for: their realm, their family, the country around them.

#region

using CalradiaRemembers.Logic.RealmTidings;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RealmTidingsRelevancePolicyTests
   {
      // STAKE: DepaHN's report: a lord never met writes because regard rose by the player's fame or deeds. He must not.
      [Test]
      public void GIVEN_a_lord_the_player_never_met_WHEN_he_holds_the_player_warmly_THEN_he_does_not_write()
      {
         RealmTidingsRelevancePolicy.MayWrite(new TidingsWriterFacts {HasMetThePlayer = false, HoldsThePlayerWarmly = true}).Should().BeFalse();
      }

      // STAKE: a friend the player knows still writes; the fix must not silence the letters people enjoy.
      [Test]
      public void GIVEN_a_lord_the_player_met_and_who_holds_them_warmly_WHEN_asked_THEN_he_may_write()
      {
         RealmTidingsRelevancePolicy.MayWrite(new TidingsWriterFacts {HasMetThePlayer = true, HoldsThePlayerWarmly = true}).Should().BeTrue();
      }

      // STAKE: "rebellions and wars in the north that have nothing to do with me": a distant bulletin is not sent.
      [Test]
      public void GIVEN_news_of_a_distant_realm_that_touches_nobody_of_the_player_WHEN_judged_THEN_it_is_not_worth_a_letter()
      {
         RealmTidingsRelevancePolicy.TouchesThePlayer(new TidingsEventFacts()).Should().BeFalse();
      }

      // STAKE: DepaHN's own list: their kingdom's affairs, their circle, the country around them, and a friend's own life.
      [Test]
      public void GIVEN_news_of_the_players_realm_kin_country_or_the_writers_house_WHEN_judged_THEN_each_is_worth_a_letter()
      {
         RealmTidingsRelevancePolicy.TouchesThePlayer(new TidingsEventFacts {InThePlayersRealm = true}).Should().BeTrue();
         RealmTidingsRelevancePolicy.TouchesThePlayer(new TidingsEventFacts {InvolvesThePlayersKin = true}).Should().BeTrue();
         RealmTidingsRelevancePolicy.TouchesThePlayer(new TidingsEventFacts {NearThePlayer = true}).Should().BeTrue();
         RealmTidingsRelevancePolicy.TouchesThePlayer(new TidingsEventFacts {AboutTheWritersHouse = true}).Should().BeTrue();
      }
   }
}
