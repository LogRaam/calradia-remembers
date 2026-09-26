// Code written by Gabriel Mailhot, 25/09/2026.
// Documents the rules of a prisoner exchange. fkasad (Nexus, 25/09/2026) holds captive lords rather than selling them
// and asked for exchanges; Gabriel ruled the same day that only heroes change hands (lords, vassals, companions, never
// troops) and that the offer always comes through an envoy. These rules decide what an envoy may propose and what the
// swap costs, so a wrong answer here is a bargain the player can see is absurd, or a soldier traded like a lord.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PrisonerExchangePolicyTests
   {
      // ── Who may be exchanged ─────────────────────────────────────────────

      // Gabriel's ruling: lords (vassals are lords) and companions are what an exchange is for.
      [Test]
      public void GIVEN_a_captive_lord_or_companion_WHEN_asked_THEN_they_may_be_exchanged()
      {
         PrisonerExchangePolicy.IsExchangeable(Lord("lord_1", 1000)).Should().BeTrue();
         PrisonerExchangePolicy.IsExchangeable(new ExchangeCaptiveFacts {HeroId = "wanderer_1", IsHero = true, IsCompanion = true}).Should().BeTrue();
      }

      // Gabriel's ruling: never troops. A soldier in a prison roster is not a hero, and is not a bargaining piece.
      [Test]
      public void GIVEN_a_captive_soldier_WHEN_asked_THEN_he_is_never_exchanged()
      {
         PrisonerExchangePolicy.IsExchangeable(new ExchangeCaptiveFacts {IsHero = false, IsLord = false}).Should().BeFalse();
      }

      // Outside the ruling: a notable is a hero but neither lord nor companion; a child is never traded; the dead are
      // nobody's to trade.
      [Test]
      public void GIVEN_a_notable_a_child_or_the_dead_WHEN_asked_THEN_none_of_them_is_exchanged()
      {
         PrisonerExchangePolicy.IsExchangeable(new ExchangeCaptiveFacts {IsHero = true}).Should().BeFalse();
         PrisonerExchangePolicy.IsExchangeable(new ExchangeCaptiveFacts {IsHero = true, IsLord = true, IsChild = true}).Should().BeFalse();
         PrisonerExchangePolicy.IsExchangeable(new ExchangeCaptiveFacts {IsHero = true, IsLord = true, IsAlive = false}).Should().BeFalse();
      }

      // ── What evens the swap ──────────────────────────────────────────────

      // Two captives of near-equal worth are a straight swap: haggling over a few denars between two lords would read
      // as petty, and the tolerance is what makes most exchanges clean.
      [Test]
      public void GIVEN_two_captives_of_near_equal_worth_WHEN_the_swap_is_priced_THEN_no_gold_changes_hands()
      {
         PrisonerExchangePolicy.GoldToSettle(playerGivesAnchor: 1000, playerReceivesAnchor: 800).Should().Be(0);
      }

      // The player gives up a great lord for a minor one: the other side pays the whole difference, so an exchange
      // is never a worse deal than the ransom the player would otherwise have been offered.
      [Test]
      public void GIVEN_the_player_gives_the_dearer_captive_WHEN_the_swap_is_priced_THEN_the_other_side_pays_the_difference()
      {
         PrisonerExchangePolicy.GoldToSettle(playerGivesAnchor: 3000, playerReceivesAnchor: 1000).Should().Be(2000);
      }

      // And the other way: getting back a dear companion for a lesser lord costs the player the difference.
      [Test]
      public void GIVEN_the_player_receives_the_dearer_captive_WHEN_the_swap_is_priced_THEN_the_player_pays_the_difference()
      {
         PrisonerExchangePolicy.GoldToSettle(playerGivesAnchor: 1000, playerReceivesAnchor: 3000).Should().Be(-2000);
      }

      // An unreadable worth prices at the same fallback the sale verbs use, so a swap and a sale agree on the captive.
      [Test]
      public void GIVEN_a_captive_whose_worth_cannot_be_read_WHEN_the_swap_is_priced_THEN_it_counts_at_the_sale_fallback()
      {
         PrisonerExchangePolicy.GoldToSettle(0, PrisonerTradePolicy.FallbackAnchor).Should().Be(0);
      }

      // ── What the other side offers ───────────────────────────────────────

      // The envoy puts on the table the captive closest in worth to the one his lord wants back, so the gold that
      // evens the deal stays as small as the prisoners allow.
      [Test]
      public void GIVEN_several_captives_the_other_side_holds_WHEN_it_chooses_what_to_offer_THEN_it_is_the_closest_in_worth()
      {
         var held = new List<ExchangeCaptiveFacts> {Lord("lord_cheap", 500), Lord("lord_close", 1900), Lord("lord_dear", 5000)};

         PrisonerExchangePolicy.ChooseCounterpart(held, wantedAnchor: 2000).HeroId.Should().Be("lord_close");
      }

      // A soldier is never offered, even when he is all they hold or the closest in worth: no exchange is proposed
      // at all rather than a trooper traded for a lord.
      [Test]
      public void GIVEN_the_other_side_holds_only_troops_WHEN_it_chooses_what_to_offer_THEN_it_offers_nobody()
      {
         var held = new List<ExchangeCaptiveFacts> {new() {IsHero = false, RansomAnchor = 2000}};

         PrisonerExchangePolicy.ChooseCounterpart(held, 2000).Should().BeNull();
      }

      private static ExchangeCaptiveFacts Lord(string id, int anchor)
         => new() {HeroId = id, IsHero = true, IsLord = true, RansomAnchor = anchor};
   }
}
