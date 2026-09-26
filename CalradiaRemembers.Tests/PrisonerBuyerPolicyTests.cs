// Code written by Gabriel Mailhot, 25/09/2026.
// Documents who may send an envoy about a captive the player holds (prisoner exchange, increment 4). fkasad (Nexus,
// 25/09/2026): "one of my allies who, hating this prisoner, would like to buy him from me. Or a father wishing to buy back
// his daughter (a lady who changed clans upon marriage)." Neither could ever come before: only the captive's clan leader,
// else his realm's leader. This order decides whose envoy the player meets, and whether the captive goes home or into
// another's cells.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PrisonerBuyerPolicyTests
   {
      private static PrisonerBuyerCandidate C(string id, PrisonerBuyerRole role, int relation = 0, bool canTreat = true)
         => new() {HeroId = id, Role = role, RelationToCaptive = relation, CanTreat = canTreat};

      // fkasad's father: his daughter married into another house, so her clan leader is her husband's kin. Her own
      // father now comes, after her house and before her realm.
      [Test]
      public void GIVEN_a_daughter_married_into_another_house_WHEN_buyers_are_ordered_THEN_her_father_comes_after_her_house_and_before_her_realm()
      {
         List<PrisonerBuyerCandidate> order = PrisonerBuyerPolicy.Order(new[] {
            C("king", PrisonerBuyerRole.Realm), C("father", PrisonerBuyerRole.Parent), C("husbands_house", PrisonerBuyerRole.House)
         });

         order.Select(c => c.HeroId).Should().Equal("husbands_house", "father", "king");
      }

      // fkasad's ally: a lord who bitterly hates the captive may want him, and the most hateful comes first.
      [Test]
      public void GIVEN_lords_who_bitterly_hate_the_captive_WHEN_buyers_are_ordered_THEN_they_may_come_the_most_hateful_first()
      {
         List<PrisonerBuyerCandidate> order = PrisonerBuyerPolicy.Order(new[] {
            C("resents", PrisonerBuyerRole.Enemy, VanillaRelation.BitterTier), C("loathes", PrisonerBuyerRole.Enemy, -80)
         });

         order.Select(c => c.HeroId).Should().Equal("loathes", "resents");
      }

      // Mere dislike is not a reason to pay gold for a man: below the bitter-enemy tier, nobody's buyer.
      [Test]
      public void GIVEN_a_lord_who_only_dislikes_the_captive_WHEN_buyers_are_ordered_THEN_he_is_not_one()
      {
         PrisonerBuyerPolicy.Order(new[] {C("cool", PrisonerBuyerRole.Enemy, VanillaRelation.EnemyTier)}).Should().BeEmpty();
      }

      // A lord who cannot treat (a prisoner himself, riding with the player, dead) sends no envoy, whoever he is.
      [Test]
      public void GIVEN_a_lord_who_cannot_treat_WHEN_buyers_are_ordered_THEN_he_is_left_out()
      {
         PrisonerBuyerPolicy.Order(new[] {C("captive_king", PrisonerBuyerRole.Realm, canTreat: false)}).Should().BeEmpty();
      }

      // One lord can be two things (a house head who is also the ruler): he is listed once, as the nearer tie.
      [Test]
      public void GIVEN_a_lord_who_is_both_house_and_realm_WHEN_buyers_are_ordered_THEN_he_is_listed_once_as_the_house()
      {
         List<PrisonerBuyerCandidate> order = PrisonerBuyerPolicy.Order(new[] {C("ruler", PrisonerBuyerRole.Realm), C("ruler", PrisonerBuyerRole.House)});

         order.Should().ContainSingle().Which.Role.Should().Be(PrisonerBuyerRole.House);
      }

      // Family and realm want the captive home and free; an enemy wants him in his own cells, which decides whether
      // an exchange frees him or merely moves him.
      [Test]
      public void GIVEN_each_kind_of_buyer_WHEN_asked_what_he_wants_THEN_only_an_enemy_wants_the_captive_kept()
      {
         PrisonerBuyerPolicy.WantsCaptiveBack(PrisonerBuyerRole.Parent).Should().BeTrue();
         PrisonerBuyerPolicy.WantsCaptiveBack(PrisonerBuyerRole.Enemy).Should().BeFalse();
      }

      // The envoy's errand names the tie truthfully: "his own daughter" is what makes the father's envoy a father's.
      [Test]
      public void GIVEN_a_father_WHEN_the_stake_is_worded_THEN_it_names_her_as_his_daughter()
      {
         PrisonerBuyerPolicy.Stake(PrisonerBuyerRole.Parent, "Derthert", captiveIsFemale: true).Should().Be("who is Derthert's own daughter");
         PrisonerBuyerPolicy.Stake(PrisonerBuyerRole.Enemy, "Derthert", false).Should().Be("whom Derthert bitterly hates");
      }
   }
}
