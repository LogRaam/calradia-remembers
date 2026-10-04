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

      // The natural exchange is the player's own companion for the lord's kinsman: offered first, even when a stranger
      // the lord holds is closer in worth. A swap that frees some third lord would read as the envoy missing the point.
      [Test]
      public void GIVEN_the_other_side_holds_one_of_the_players_people_WHEN_it_chooses_what_to_offer_THEN_that_one_comes_first()
      {
         var held = new List<ExchangeCaptiveFacts> {
            Lord("stranger_close", 2000),
            new() {HeroId = "players_companion", IsHero = true, IsCompanion = true, RansomAnchor = 600, BelongsToPlayer = true}
         };

         PrisonerExchangePolicy.ChooseCounterpart(held, 2000).HeroId.Should().Be("players_companion");
      }

      // tashmetu (2.6.4): the envoy offered, as an exchange, "another Rex Derthert": the very captive it came for. The one
      // wanted is never his own counterpart.
      [Test]
      public void GIVEN_the_wanted_captive_among_their_hold_WHEN_it_chooses_what_to_offer_THEN_it_never_offers_him_for_himself()
      {
         ExchangeCaptiveFacts himself = Lord("derthert", 2000);
         himself.IsTheWantedCaptive = true;

         PrisonerExchangePolicy.ChooseCounterpart(new List<ExchangeCaptiveFacts> {himself}, 2000).Should().BeNull();
         PrisonerExchangePolicy.ChooseCounterpart(new List<ExchangeCaptiveFacts> {himself, Lord("other", 900)}, 2000).HeroId.Should().Be("other");
      }

      // The same fault in a private word (found with tashmetu's report, 27/09/2026): for a member of the player's own
      // clan, "the captives his clan holds" ARE the player's, so the player was offered his own prisoners in exchange for
      // his own prisoners. There is no other side to trade with inside one house.
      [Test]
      public void GIVEN_someone_of_the_players_own_clan_WHEN_asked_whether_captives_can_be_exchanged_with_them_THEN_they_cannot()
      {
         PrisonerExchangePolicy.IsAnotherSide(npcIsOfPlayersClan: true).Should().BeFalse();
         PrisonerExchangePolicy.IsAnotherSide(npcIsOfPlayersClan: false).Should().BeTrue();
      }

      // ── When an envoy may come ───────────────────────────────────────────

      // The envoy replaces a letter, and a letter was never sent twice about one captive in quick succession: one
      // envoy, then a while before the next, whether he was heard or turned away.
      [Test]
      public void GIVEN_an_envoy_came_about_a_captive_recently_WHEN_another_would_come_THEN_he_waits()
      {
         PrisonerExchangePolicy.EnvoyMayCome(lastEnvoyDay: 100, today: 100 + PrisonerExchangePolicy.EnvoyCooldownDays - 1).Should().BeFalse();
         PrisonerExchangePolicy.EnvoyMayCome(100, 100 + PrisonerExchangePolicy.EnvoyCooldownDays).Should().BeTrue();
      }

      // The first envoy about a captive needs no wait.
      [Test]
      public void GIVEN_no_envoy_ever_came_about_a_captive_WHEN_one_would_come_THEN_he_may()
      {
         PrisonerExchangePolicy.EnvoyMayCome(null, 5).Should().BeTrue();
      }

      // aphareish (Nexus, 04/10/2026): "ransom couriers being triggered merely seconds after capture". Word of a capture
      // must reach the lord, and his envoy ride to the player, before anyone can come to treat: never the day of the battle.
      [Test]
      public void GIVEN_a_captive_just_taken_WHEN_an_envoy_would_come_THEN_he_waits_for_the_news_to_travel()
      {
         PrisonerExchangePolicy.NewsHasReachedHisPeople(daysHeld: 0).Should().BeFalse();
         PrisonerExchangePolicy.NewsHasReachedHisPeople(PrisonerExchangePolicy.EnvoyDelayAfterCaptureDays - 1).Should().BeFalse();
      }

      // Once the news has had time to travel, an envoy may come (the cooldown still applies after that).
      [Test]
      public void GIVEN_a_captive_held_for_some_days_WHEN_an_envoy_would_come_THEN_he_may()
      {
         PrisonerExchangePolicy.NewsHasReachedHisPeople(PrisonerExchangePolicy.EnvoyDelayAfterCaptureDays).Should().BeTrue();
      }

      // ── What becomes of each captive ─────────────────────────────────────

      // The whole point of an exchange (fkasad: "a father wishing to buy back his daughter"): the lord gets his own
      // back, free. Leaving her a prisoner in her own father's dungeon would be absurd.
      [Test]
      public void GIVEN_a_captive_handed_to_his_own_side_WHEN_the_exchange_is_done_THEN_he_goes_free()
      {
         PrisonerExchangePolicy.HandoverTo(captiveBelongsToReceiver: true).Should().Be(ExchangeHandover.Freed);
      }

      // A captive handed to a side he does not belong to is not freed by being traded: he changes gaolers, which is
      // what an ally who hates him is paying for.
      [Test]
      public void GIVEN_a_captive_handed_to_a_side_not_his_own_WHEN_the_exchange_is_done_THEN_he_stays_a_prisoner_in_their_hands()
      {
         PrisonerExchangePolicy.HandoverTo(captiveBelongsToReceiver: false).Should().Be(ExchangeHandover.IntoCustody);
      }

      private static ExchangeCaptiveFacts Lord(string id, int anchor)
         => new() {HeroId = id, IsHero = true, IsLord = true, RansomAnchor = anchor};
   }
}
