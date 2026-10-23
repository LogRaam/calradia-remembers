// Code written by Gabriel Mailhot, 26/09/2026.
// Documents outlawry, bounties and amnesty (arrests Inc 5; Gabriel's rulings, 26/09/2026). The stakes: a bounty from
// the wrong realm would set the whole map on a player who robbed one caravan; a sovereign branded a brigand breaks the
// line Gabriel drew; a bounty that never ended, or ended too easily, would make outlawry either a life sentence or
// nothing at all. froggyluv (22/10/2026) found the other side of it: the option turned off and the hunters still came,
// even at a 2000-strong army, with no way to settle; Gabriel (23/10/2026): off lifts the bounties, no hunters against a
// player in another's army, and a bounty can be paid to a lord of the realm.

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

      // Hunters ride in a number close to the player's own, so the fight is real but winnable, within bounds.
      [TestCase(5, OutlawPolicy.MinHunters)]
      [TestCase(50, 40)]
      [TestCase(500, OutlawPolicy.MaxHunters)]
      public void GIVEN_the_players_number_WHEN_a_realm_sends_hunters_THEN_they_ride_close_to_it_within_bounds(int playerSize, int expected)
      {
         OutlawPolicy.HunterStrength(playerSize).Should().Be(expected);
      }

      // A lost band is not replaced the next morning.
      [Test]
      public void GIVEN_a_lost_band_WHEN_new_hunters_are_asked_for_THEN_they_come_only_after_the_cooldown()
      {
         OutlawPolicy.MaySendHunters(null, 5).Should().BeTrue();
         OutlawPolicy.MaySendHunters(100, 100 + OutlawPolicy.HunterCooldownDays - 1).Should().BeFalse();
         OutlawPolicy.MaySendHunters(100, 100 + OutlawPolicy.HunterCooldownDays).Should().BeTrue();
      }

      // The bounty's cause is the charge at trial: a flight, or brigandage.
      [Test]
      public void GIVEN_a_bounty_WHEN_its_captive_is_tried_THEN_its_cause_is_the_charge()
      {
         OutlawPolicy.ChargeFor(BountyReason.Flight).Should().Be(ArrestCharge.FledArrest);
         OutlawPolicy.ChargeFor(BountyReason.Brigandage).Should().Be(ArrestCharge.Brigandage);
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

      // STAKE: froggyluv: "I've disabled that in options but they're still showing up". Off must mean no price stands.
      [Test]
      public void GIVEN_the_option_turned_off_WHEN_the_player_roams_free_THEN_the_bounties_are_lifted()
      {
         OutlawPolicy.BountiesLiftedWhenOff(arrestsOn: false, lawHoldsThePlayer: false).Should().BeTrue();
      }

      // STAKE: the option's own promise since 26/09: turning it off never strands a player mid-trial or in the cells.
      [Test]
      public void GIVEN_the_option_turned_off_WHEN_the_law_already_holds_the_player_THEN_the_case_runs_to_its_end()
      {
         OutlawPolicy.BountiesLiftedWhenOff(arrestsOn: false, lawHoldsThePlayer: true).Should().BeFalse();
      }

      // STAKE: with the option on, a bounty stands until paid, pardoned or collected.
      [Test]
      public void GIVEN_the_option_on_WHEN_the_hour_passes_THEN_no_bounty_is_lifted()
      {
         OutlawPolicy.BountiesLiftedWhenOff(arrestsOn: true, lawHoldsThePlayer: false).Should().BeFalse();
      }

      // STAKE: froggyluv: the hunters "will charge me no matter what, even if I'm part of a 2000+ strong army".
      [Test]
      public void GIVEN_the_player_marching_in_another_lords_army_WHEN_the_hunters_would_ride_THEN_they_hold_off()
      {
         OutlawPolicy.HuntersMayRide(new HuntFacts {ArrestsOn = true, InAnotherLeadersArmy = true}).Should().BeFalse();
      }

      // STAKE: a player alone on the road, or leading their own army, is still hunted: the bounty stays a real danger.
      [Test]
      public void GIVEN_the_player_on_their_own_WHEN_the_hunters_would_ride_THEN_they_ride()
      {
         OutlawPolicy.HuntersMayRide(new HuntFacts {ArrestsOn = true}).Should().BeTrue();
      }

      // STAKE: the option off, not one band rides, whatever the save holds.
      [Test]
      public void GIVEN_the_option_off_WHEN_the_hunters_would_ride_THEN_they_do_not()
      {
         OutlawPolicy.HuntersMayRide(new HuntFacts {ArrestsOn = false}).Should().BeFalse();
      }

      // STAKE: Gabriel: the bounty can be paid to a lord of the realm. Defying the sovereign's own law costs more than
      // robbing his merchants; both are within a mid-game purse, so paying is a real choice, not a formality.
      [Test]
      public void GIVEN_each_cause_of_a_bounty_WHEN_the_player_pays_it_off_THEN_a_flight_costs_more_than_brigandage()
      {
         OutlawPolicy.BountyPrice(BountyReason.Brigandage).Should().Be(OutlawPolicy.BrigandageBountyPrice);
         OutlawPolicy.BountyPrice(BountyReason.Flight).Should().Be(OutlawPolicy.FlightBountyPrice);
         OutlawPolicy.FlightBountyPrice.Should().BeGreaterThan(OutlawPolicy.BrigandageBountyPrice);
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
