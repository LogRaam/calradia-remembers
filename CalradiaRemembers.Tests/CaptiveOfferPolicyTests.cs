// Code written by Gabriel Mailhot, 14/10/2026.
// What this decides: what a captive of the player may offer for her freedom, and how readily she offers it.
//
// Maifailun (Nexus, 14/10/2026): he held Adrichea (Generosity, Honor and Mercy all -1), a clan leader, and asked her to
// swear fealty to Rhagaea or spend a night with him. She answered "kill me if you must", "the most virtuous act I could
// possibly come up with", and nothing in CR could hold a counter-offer anyway. Gabriel (14/10/2026): the captive proposes
// terms herself; in Mature and Hardcore, intimacy is a bargaining term, since those players are adults who want to explore
// sexualised roleplay, and a manipulative, honorless NPC uses it without scruple, especially for an advantage. The terms
// are only those the game can carry out for HER, decided before she speaks (NPCs never lie, never promise what the game
// refuses). If this is wrong: an honorless captive plays the martyr again, a proud one sells herself, or a captive offers
// a ransom she cannot pay, an oath the game cannot take, or intimacy where the player set none, or under age.

#region

using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CaptiveOfferPolicyTests
   {
      // Maifailun's Adrichea: no honor, no mercy, no generosity, her clan's leader, with coin, in a Mature game, both adults.
      private static CaptiveOfferFacts Adrichea(AdultContentLevel level = AdultContentLevel.Mature) => new() {
         Traits = new TraitFacts {Honor = -1, Mercy = -1, Generosity = -1},
         Fear = 30, RansomMeans = 5000, LeadsHerClan = true, PlayerServesARealm = true, HerClanIsInThatRealm = false,
         ContentLevel = level, BothOfAge = true, OrientationAllowsThePlayer = true
      };

      // THE REPORTED CASE: she has no honor to die for, so she makes the first offer, and fealty, coin and her body are
      // all on the table.
      [Test]
      public void GIVEN_an_honorless_captive_like_Adrichea_WHEN_her_offer_is_decided_THEN_she_offers_first_with_every_term_open_to_her()
      {
         CaptiveOffer offer = CaptiveOfferPolicy.Decide(Adrichea());

         offer.Disposition.Should().Be(CaptiveDisposition.OffersFirst);
         offer.Terms.Should().Contain(new[] {CaptiveTerm.Ransom, CaptiveTerm.Fealty, CaptiveTerm.Intimacy});
      }

      // A principled captive who does not fear the player may stand on her pride, and she never sells herself; she still
      // bargains in honorable coin if she bargains at all.
      [Test]
      public void GIVEN_an_honor_bound_captive_unafraid_WHEN_decided_THEN_she_stands_on_pride_and_never_offers_intimacy()
      {
         CaptiveOffer offer = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
            Traits = new TraitFacts {Honor = 2}, Fear = 10, RansomMeans = 5000, ContentLevel = AdultContentLevel.Hardcore,
            BothOfAge = true, OrientationAllowsThePlayer = true
         });

         offer.Disposition.Should().Be(CaptiveDisposition.StandsOnPride);
         offer.Terms.Should().Contain(CaptiveTerm.Ransom);
         offer.Terms.Should().NotContain(CaptiveTerm.Intimacy);
      }

      // Fear bends even pride into bargaining (the fear floor the nemesis turn uses), but not into selling herself.
      [Test]
      public void GIVEN_an_honor_bound_captive_who_fears_the_player_WHEN_decided_THEN_she_bargains_but_still_not_with_her_body()
      {
         CaptiveOffer offer = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
            Traits = new TraitFacts {Honor = 1}, Fear = NemesisTurnPolicy.FearFloor, RansomMeans = 500, ContentLevel = AdultContentLevel.Mature,
            BothOfAge = true, OrientationAllowsThePlayer = true
         });

         offer.Disposition.Should().Be(CaptiveDisposition.Bargains);
         offer.Terms.Should().NotContain(CaptiveTerm.Intimacy);
      }

      // Intimacy is a term only in Mature and above, only between adults, and only if she could want the player at all:
      // each of the three closes it on its own.
      [Test]
      public void GIVEN_content_off_or_a_minor_or_an_orientation_that_excludes_the_player_WHEN_decided_THEN_intimacy_is_no_term()
      {
         CaptiveOfferPolicy.Decide(Adrichea(AdultContentLevel.Off)).Terms.Should().NotContain(CaptiveTerm.Intimacy);

         CaptiveOfferFacts minor = Adrichea();
         CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
            Traits = minor.Traits, Fear = minor.Fear, RansomMeans = minor.RansomMeans, ContentLevel = AdultContentLevel.Hardcore,
            BothOfAge = false, OrientationAllowsThePlayer = true
         }).Terms.Should().NotContain(CaptiveTerm.Intimacy);

         CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
            Traits = minor.Traits, Fear = minor.Fear, RansomMeans = minor.RansomMeans, ContentLevel = AdultContentLevel.Hardcore,
            BothOfAge = true, OrientationAllowsThePlayer = false
         }).Terms.Should().NotContain(CaptiveTerm.Intimacy);

         CaptiveOfferPolicy.Decide(Adrichea(AdultContentLevel.Explicit)).Terms.Should().Contain(CaptiveTerm.Intimacy);
      }

      // A ransom is never above what she (or her house) can pay, and no coin means no ransom term at all.
      [Test]
      public void GIVEN_her_means_WHEN_decided_THEN_the_ransom_ceiling_is_her_means_and_no_coin_is_no_ransom()
      {
         CaptiveOfferPolicy.Decide(Adrichea()).RansomCeiling.Should().Be(5000);

         CaptiveOfferFacts broke = Adrichea();
         CaptiveOffer offer = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
            Traits = broke.Traits, Fear = broke.Fear, RansomMeans = CaptiveOfferPolicy.RansomFloor - 1, ContentLevel = broke.ContentLevel,
            BothOfAge = true, OrientationAllowsThePlayer = true
         });
         offer.Terms.Should().NotContain(CaptiveTerm.Ransom);
         offer.RansomCeiling.Should().Be(0);
      }

      // Fealty is hers to give only if she leads her house, the player serves a realm, and her house is not in it already.
      [Test]
      public void GIVEN_who_she_is_and_where_the_player_serves_WHEN_decided_THEN_fealty_is_open_only_when_the_game_can_take_it()
      {
         CaptiveOfferFacts a = Adrichea();
         CaptiveOffer notLeader = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {Traits = a.Traits, Fear = a.Fear, LeadsHerClan = false, PlayerServesARealm = true});
         CaptiveOffer noRealm = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {Traits = a.Traits, Fear = a.Fear, LeadsHerClan = true, PlayerServesARealm = false});
         CaptiveOffer already = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {Traits = a.Traits, Fear = a.Fear, LeadsHerClan = true, PlayerServesARealm = true, HerClanIsInThatRealm = true});

         new[] {notLeader, noRealm, already}.Should().OnlyContain(o => !o.Terms.Contains(CaptiveTerm.Fealty));
      }

      // Service is the existing recruit_prisoner gate (the regard floor a player report set): the counter-offer never
      // becomes a way around it.
      [Test]
      public void GIVEN_the_recruit_gate_WHEN_decided_THEN_service_is_a_term_only_when_that_gate_is_open()
      {
         CaptiveOfferFacts a = Adrichea();
         CaptiveOfferPolicy.Decide(a).Terms.Should().NotContain(CaptiveTerm.Service);
         CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {Traits = a.Traits, Fear = a.Fear, ServiceIsOpen = true}).Terms.Should().Contain(CaptiveTerm.Service);
      }

      // Found reading the decompiled engine while building the counter_offer executor (14/10/2026): leaving a
      // kingdom (ChangeKingdomAction / Clan.ClanLeaveKingdom) only clears Kingdom = null and never checks whether
      // the clan WAS that kingdom's RulingClan, so swearing a captured sovereign's house to another realm would
      // orphan the one she ruled, a state the game has no path to repair. Fealty must never be a term the game
      // cannot truthfully carry out for her, so a ruling clan's head never has it on the table at all.
      [Test]
      public void GIVEN_a_captive_who_rules_her_own_realm_WHEN_decided_THEN_fealty_is_never_a_term()
      {
         CaptiveOfferFacts a = Adrichea();
         CaptiveOffer offer = CaptiveOfferPolicy.Decide(new CaptiveOfferFacts {
            Traits = a.Traits, Fear = a.Fear, LeadsHerClan = true, PlayerServesARealm = true, HerClanIsInThatRealm = false, RulesHerRealm = true
         });

         offer.Terms.Should().NotContain(CaptiveTerm.Fealty);
      }

      // Goodwill costs nothing and is always on the table (the unconditional line every captive is told she may
      // offer), so it is never one of the gated terms Decide adds: a test enumerating every CaptiveTerm here would
      // wrongly expect it gated like the rest.
      [Test]
      public void GIVEN_any_facts_WHEN_decided_THEN_goodwill_is_never_an_emitted_term()
      {
         CaptiveOfferPolicy.Decide(Adrichea()).Terms.Should().NotContain(CaptiveTerm.Goodwill);
      }

      // ParseTerms reads a counter_offer action's "terms" parameter: the bridge must read exactly what the model
      // was taught to write (comma list, free-form wording), or a genuinely struck bargain settles nothing.
      [Test]
      public void GIVEN_a_comma_list_of_terms_WHEN_parsed_THEN_each_recognised_word_becomes_its_term()
      {
         CaptiveOfferPolicy.ParseTerms("ransom, fealty").Should().BeEquivalentTo(new[] {CaptiveTerm.Ransom, CaptiveTerm.Fealty});
         CaptiveOfferPolicy.ParseTerms("her fealty, her service").Should().BeEquivalentTo(new[] {CaptiveTerm.Fealty, CaptiveTerm.Service});
         CaptiveOfferPolicy.ParseTerms("intimacy").Should().BeEquivalentTo(new[] {CaptiveTerm.Intimacy});
      }

      // Nothing recognisable (empty, null, or an unknown word) parses to no terms, never a thrown exception.
      [Test]
      public void GIVEN_nothing_recognisable_WHEN_parsed_THEN_the_result_is_empty()
      {
         CaptiveOfferPolicy.ParseTerms(null).Should().BeEmpty();
         CaptiveOfferPolicy.ParseTerms("").Should().BeEmpty();
         CaptiveOfferPolicy.ParseTerms("her undying love").Should().BeEmpty();
      }
   }
}
