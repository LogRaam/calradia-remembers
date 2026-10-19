// Code written by Gabriel Mailhot, 14/10/2026.
// What these pin: a captive's counter_offer is always confirmed, the question names every term she struck, and
// settling it never leaves a free_prisoner or join_clan beside it to read the same bargain twice.
//
// Maifailun (Nexus, 14/10/2026): he held Adrichea (Honor -1, a clan leader), asked her to swear fealty to Rhagaea
// or spend a night with him, and she answered "kill me if you must" - nothing in CR could hold a counter-offer
// anyway. Gabriel: the captive proposes terms herself, settled through one verb. If these fail, the player is
// asked a vague question that names nothing she agreed to, or a term she never truly had on the table executes,
// or freeing her on these terms ALSO fires a stray free_prisoner/join_clan for the same bargain.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Actions
{
   [TestFixture]
   public sealed class CounterOfferTests
   {
      // ── Confirmation ──

      // A counter_offer always asks, exactly like a sale: the player sees what she is agreeing to before anything moves.
      [Test]
      public void GIVEN_a_counter_offer_WHEN_asked_THEN_it_requires_confirmation()
      {
         ActionConfirmationPolicy.RequiresConfirmation("counter_offer").Should().BeTrue();
      }

      // Maifailun's own case, in words: "Accept Adrichea's offer (swear her house to the Northern Empire, 3000
      // coins) and set her free?" The question names EVERY term, in the order she struck them.
      [Test]
      public void GIVEN_fealty_and_a_ransom_WHEN_the_question_is_built_THEN_it_names_both_in_order()
      {
         string question = ActionConfirmationPolicy.BuildOfferQuestion(new OfferConfirmationFacts {
            NpcName = "Adrichea", IsFemale = true, Terms = new[] {CaptiveTerm.Fealty, CaptiveTerm.Ransom},
            Ransom = 3000, FealtyRealm = "the Northern Empire"
         });

         question.Should().Be("Accept Adrichea's offer (swear her house to the Northern Empire, 3000 coins) and set her free?");
      }

      // A ransom brought down to the payer's purse is said, so the player is not surprised by a smaller sum
      // (mirrors ItemSaleTests's "all they have" for a sale).
      [Test]
      public void GIVEN_a_ransom_clamped_to_the_payers_purse_WHEN_asked_THEN_the_question_says_so()
      {
         string question = ActionConfirmationPolicy.BuildOfferQuestion(new OfferConfirmationFacts {
            NpcName = "Adrichea", IsFemale = true, Terms = new[] {CaptiveTerm.Ransom}, Ransom = 400, RansomClampedToPurse = true
         });

         question.Should().Be("Accept Adrichea's offer (400 coins (all they have)) and set her free?");
      }

      // A captive held prisoner is not always female: the question must say "him", never default to "her".
      [Test]
      public void GIVEN_a_male_captive_WHEN_asked_THEN_the_question_uses_his_pronoun()
      {
         string question = ActionConfirmationPolicy.BuildOfferQuestion(new OfferConfirmationFacts {
            NpcName = "Lord Boyar Vsevolod", IsFemale = false, Terms = new[] {CaptiveTerm.Service}
         });

         question.Should().Be("Accept Lord Boyar Vsevolod's offer (take service in your clan) and set him free?");
      }

      // No terms at all (an empty or unparseable offer) still asks something sane rather than an empty parenthesis.
      [Test]
      public void GIVEN_no_terms_WHEN_asked_THEN_the_question_names_her_bare_word()
      {
         ActionConfirmationPolicy.BuildOfferQuestion(new OfferConfirmationFacts {NpcName = "Adrichea", IsFemale = true})
                                  .Should().Be("Accept Adrichea's offer (her word alone) and set her free?");
      }

      // ── One payment per deal ──

      // A ransom settled by counter_offer carries its own coin: a give_gold/take_gold beside it ("I will pay it
      // now") is the same payment written twice, exactly like every other deal that moves its own coin.
      [TestCase("give_gold")]
      [TestCase("take_gold")]
      public void GIVEN_a_counter_offer_WHEN_a_separate_payment_is_also_emitted_THEN_it_is_the_same_payment(string payment)
      {
         TurnPaymentPolicy.IsSamePaymentAsADealThisTurn(payment, new[] {"counter_offer", payment}).Should().BeTrue();
      }

      // ── Settled once, never read twice ──

      // THE REPORTED SHAPE, mirrored from sell_item's own fault: a model that writes counter_offer ALSO writing
      // free_prisoner or join_clan for the very same bargain must not settle it twice (once by the new verb's own
      // executor, once by the old one it replaces).
      [Test]
      public void GIVEN_a_counter_offer_WHEN_free_prisoner_or_join_clan_ride_beside_it_THEN_both_are_dropped()
      {
         TradeBatchPolicy.SupersededByCounterOffer(new List<string> {"counter_offer", "free_prisoner", "join_clan", "change_relation"})
                         .Should().BeEquivalentTo(new[] {1, 2});
      }

      // recruit_prisoner beside the offer would free and recruit her before the player answered the offer, and the
      // confirmed offer would then find her free: the offer is the deal, so it goes too.
      [Test]
      public void GIVEN_a_counter_offer_WHEN_recruit_prisoner_rides_beside_it_THEN_it_is_dropped()
      {
         TradeBatchPolicy.SupersededByCounterOffer(new List<string> {"recruit_prisoner", "counter_offer"}).Should().BeEquivalentTo(new[] {0});
      }

      // Without a counter_offer in the turn, free_prisoner and join_clan stand exactly as before (an ordinary
      // mercy release, or an unrelated lord joining the clan).
      [Test]
      public void GIVEN_no_counter_offer_WHEN_settled_THEN_nothing_is_dropped()
      {
         TradeBatchPolicy.SupersededByCounterOffer(new List<string> {"free_prisoner", "join_clan"}).Should().BeEmpty();
      }

      // A different deed in the same turn as a counter_offer is its own, and stays.
      [Test]
      public void GIVEN_a_counter_offer_and_an_unrelated_action_WHEN_settled_THEN_the_unrelated_action_stays()
      {
         TradeBatchPolicy.SupersededByCounterOffer(new List<string> {"counter_offer", "change_relation"}).Should().BeEmpty();
      }
   }
}
