// Code written by Gabriel Mailhot, 01/11/2026.
// CaptivityRansomPolicy decides whether the game's own ransom offer reaches a captive player. If it is wrong, a lord at Hardcore
// never bargains (Windmolen, 26/10/2026: "just ... ransom you"), or a band sells a captive the scenes were meant to hold.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class CaptivityRansomPolicyTests
   {
      [Test]
      public void GIVEN_a_lord_holding_the_player_at_hardcore_WHEN_the_game_offers_a_ransom_THEN_the_player_sees_it()
      {
         // STAKE: Windmolen's report: the line said bandits do not bargain, and it silenced lords too.
         CaptivityRansomPolicy.Suppresses(new RansomOfferFacts {IsHardcore = true}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_bandits_at_hardcore_WHEN_the_game_offers_a_ransom_THEN_it_is_cancelled()
      {
         // STAKE: the rule's first purpose (VanillaCaptivityPatch, 14/06/2026): a ransom cut a bandit captivity short before any scene.
         CaptivityRansomPolicy.Suppresses(new RansomOfferFacts {IsHardcore = true, CaptorIsBandit = true}).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_nemesis_or_an_ambush_convoy_at_hardcore_WHEN_the_game_offers_a_ransom_THEN_it_is_cancelled()
      {
         // STAKE: both are arcs with their own end: a convoy on the road delivers the player to the lord who paid for them.
         CaptivityRansomPolicy.Suppresses(new RansomOfferFacts {IsHardcore = true, CaptorIsNemesis = true}).Should().BeTrue();
         CaptivityRansomPolicy.Suppresses(new RansomOfferFacts {IsHardcore = true, InAmbushConvoy = true}).Should().BeTrue();
      }

      [Test]
      public void GIVEN_any_level_below_hardcore_WHEN_bandits_offer_a_ransom_THEN_it_stands()
      {
         // STAKE: VanillaCaptivityPatch's promise: lower content levels keep their full vanilla behavior.
         CaptivityRansomPolicy.Suppresses(new RansomOfferFacts {CaptorIsBandit = true}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_player_held_by_the_law_WHEN_the_game_offers_a_ransom_THEN_it_is_cancelled_at_every_level()
      {
         // STAKE: arrests, 26/09/2026: a player held for trial, or serving a sentence, is never ransomed free by the captor.
         CaptivityRansomPolicy.Suppresses(new RansomOfferFacts {HeldByTheLaw = true}).Should().BeTrue();
      }
   }
}
