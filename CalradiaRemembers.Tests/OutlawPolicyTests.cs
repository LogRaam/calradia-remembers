// Code written by Gabriel Mailhot, 26/09/2026.
// Documents outlawry, bounties and amnesty (arrests Inc 5; Gabriel's rulings, 26/09/2026). The stakes: a bounty from
// the wrong realm would set the whole map on a player who robbed one caravan; a sovereign branded a brigand breaks the
// line Gabriel drew; a bounty that never ended, or ended too easily, would make outlawry either a life sentence or
// nothing at all.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class OutlawPolicyTests
   {
      // Gabriel: a merchant attacked, the realm that sent it and the realm it was bound for both want the brigand.
      [Test]
      public void GIVEN_a_merchant_attacked_WHEN_the_bounty_is_placed_THEN_the_sender_and_the_receiver_want_the_player()
      {
         OutlawPolicy.BrigandageRealms(new BrigandageFacts {OwnerRealmId = "vlandia", DestinationRealmId = "sturgia"})
                     .Should().Equal("vlandia", "sturgia");
      }

      // Gabriel: a fishing boat, only its owner.
      [Test]
      public void GIVEN_a_fishing_boat_attacked_WHEN_the_bounty_is_placed_THEN_only_its_owner_wants_the_player()
      {
         OutlawPolicy.BrigandageRealms(new BrigandageFacts {IsFishing = true, OwnerRealmId = "nord", DestinationRealmId = "sturgia"})
                     .Should().Equal("nord");
      }

      // A merchant trading within its own realm is one realm's grievance, not two.
      [Test]
      public void GIVEN_a_merchant_bound_for_its_own_realm_WHEN_the_bounty_is_placed_THEN_one_realm_wants_the_player()
      {
         OutlawPolicy.BrigandageRealms(new BrigandageFacts {OwnerRealmId = "vlandia", DestinationRealmId = "vlandia"})
                     .Should().Equal("vlandia");
         OutlawPolicy.BrigandageRealms(new BrigandageFacts {OwnerRealmId = "vlandia"}).Should().Equal("vlandia");
      }

      // Gabriel: never a sovereign; and a realm already at war is at war, not robbed.
      [Test]
      public void GIVEN_who_attacks_whom_WHEN_judged_THEN_only_a_non_ruler_at_peace_is_a_brigand()
      {
         OutlawPolicy.IsBrigandage(playerRulesAKingdom: false, atWarWithThatRealm: false).Should().BeTrue();
         OutlawPolicy.IsBrigandage(playerRulesAKingdom: true, atWarWithThatRealm: false).Should().BeFalse();
         OutlawPolicy.IsBrigandage(playerRulesAKingdom: false, atWarWithThatRealm: true).Should().BeFalse();
      }

      // The game's own outlaw rule wars on an independent clan; a sworn vassal cannot be warred on alone.
      [Test]
      public void GIVEN_the_players_standing_WHEN_hunted_THEN_an_independent_clan_is_warred_on_and_a_vassal_gets_hunters()
      {
         OutlawPolicy.ModeFor(playerLeadsAnIndependentClan: true).Should().Be(HuntMode.War);
         OutlawPolicy.ModeFor(playerLeadsAnIndependentClan: false).Should().Be(HuntMode.OutlawHunters);
      }

      // Gabriel: a bounty ends only by an amnesty, and an amnesty is rare.
      [Test]
      public void GIVEN_a_weekly_roll_WHEN_an_amnesty_is_asked_THEN_it_comes_only_on_a_rare_roll()
      {
         OutlawPolicy.AmnestyDeclared(0.005).Should().BeTrue();
         OutlawPolicy.AmnestyDeclared(OutlawPolicy.AmnestyWeeklyChance).Should().BeFalse();
         OutlawPolicy.AmnestyDeclared(0.5).Should().BeFalse();
      }

      // One price per realm: a second crime against the same realm does not double the hunt.
      [Test]
      public void GIVEN_a_realm_with_a_bounty_already_WHEN_another_is_placed_THEN_it_keeps_one()
      {
         var bounties = new List<Bounty>();
         OutlawPolicy.Place(bounties, "vlandia", BountyReason.Brigandage, 10);
         OutlawPolicy.Place(bounties, "vlandia", BountyReason.Flight, 20);
         OutlawPolicy.Place(bounties, "sturgia", BountyReason.Brigandage, 20);

         bounties.Should().HaveCount(2);
         bounties[0].PlacedDay.Should().Be(10);
      }

      // Gabriel: a captured pirate goes to the sovereign who wanted them, and is tried there, charges and all.
      [Test]
      public void GIVEN_an_outlaw_WHEN_caught_on_a_realms_bounty_THEN_that_sovereign_tries_them_on_its_charges()
      {
         var c = new ArrestCase();
         c.CaughtOnBounty("king_of_sturgia", new[] {ArrestCharge.Brigandage});

         c.Stage.Should().Be(ArrestStage.OnTrial);
         c.LiegeId.Should().Be("king_of_sturgia");
         c.Charges.Should().Equal(ArrestCharge.Brigandage);
         c.BountyOnHead.Should().BeFalse();
      }

      // An amnesty from the realm the player fled ends the outlawry outright.
      [Test]
      public void GIVEN_an_outlaw_WHEN_their_realm_pardons_them_THEN_the_case_is_closed()
      {
         var c = new ArrestCase();
         c.Charge(ArrestCharge.TreasonLetter, "king", true);
         c.Flee();
         c.Pardon();

         c.Stage.Should().Be(ArrestStage.None);
         c.Charges.Should().BeEmpty();
      }
   }
}
