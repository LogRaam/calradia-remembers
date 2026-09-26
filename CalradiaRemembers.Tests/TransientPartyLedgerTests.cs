// Code written by Gabriel Mailhot, 26/09/2026.
// Documents the bookkeeping behind the mod's transient parties (couriers, sergeants, bounty hunters), the one service
// that replaces the same shape copied three times (the event-listeners map's proposal, 26/09/2026). The stakes: a party
// forgotten by the ledger is a party a save would carry (a mod party in the save file), and a party remembered after it
// is gone makes its owner steer a ghost.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TransientPartyLedgerTests
   {
      // What a save must despawn: everything tracked, whatever its role.
      [Test]
      public void GIVEN_parties_of_several_roles_WHEN_a_save_asks_THEN_every_one_is_listed()
      {
         var ledger = new TransientPartyLedger();
         ledger.Track("p2", "arrest.hunters", "vlandia");
         ledger.Track("p1", "arrest.sergeants");

         ledger.AllIds().Should().Equal("p1", "p2");
      }

      // An owner finds its own parties by role, and a band of hunters by the realm it answers to.
      [Test]
      public void GIVEN_tracked_parties_WHEN_an_owner_looks_by_role_or_tag_THEN_it_finds_only_its_own()
      {
         var ledger = new TransientPartyLedger();
         ledger.Track("s1", "arrest.sergeants");
         ledger.Track("h1", "arrest.hunters", "vlandia");
         ledger.Track("h2", "arrest.hunters", "sturgia");

         ledger.OfRole("arrest.hunters").Should().HaveCount(2);
         ledger.WithTag("arrest.hunters", "sturgia").PartyId.Should().Be("h2");
         ledger.WithTag("arrest.hunters", "khuzait").Should().BeNull();
         ledger.Find("s1").Role.Should().Be("arrest.sergeants");
      }

      // A party that is not one of ours is not found: a real destroyed party is never mistaken for a courier.
      [Test]
      public void GIVEN_a_party_the_mod_never_tracked_WHEN_looked_up_THEN_it_is_not_ours()
      {
         new TransientPartyLedger().Find("lord_party_1").Should().BeNull();
      }

      // Tracking the same id again replaces it, so an owner re-sending a band never holds two entries for one party.
      [Test]
      public void GIVEN_a_party_tracked_twice_WHEN_counted_THEN_it_is_one_entry_with_the_latest_role()
      {
         var ledger = new TransientPartyLedger();
         ledger.Track("p", "arrest.sergeants");
         ledger.Track("p", "arrest.hunters", "vlandia");

         ledger.Count.Should().Be(1);
         ledger.Find("p").Role.Should().Be("arrest.hunters");
      }

      // Parties the world has lost without the mod seeing it are dropped, so no owner steers a ghost.
      [Test]
      public void GIVEN_parties_gone_from_the_world_WHEN_swept_THEN_they_are_forgotten()
      {
         var ledger = new TransientPartyLedger();
         ledger.Track("alive", "arrest.sergeants");
         ledger.Track("gone", "arrest.hunters", "vlandia");

         ledger.ForgetMissing(id => id == "alive").Should().Be(1);
         ledger.AllIds().Should().Equal("alive");
      }

      // Blank ids or roles are never tracked: an entry nobody can find is an entry nobody despawns.
      [Test]
      public void GIVEN_a_blank_id_or_role_WHEN_tracked_THEN_nothing_is_recorded()
      {
         var ledger = new TransientPartyLedger();
         ledger.Track("", "arrest.sergeants");
         ledger.Track("p", " ");

         ledger.Count.Should().Be(0);
         ledger.Forget("p").Should().BeFalse();
      }
   }
}
