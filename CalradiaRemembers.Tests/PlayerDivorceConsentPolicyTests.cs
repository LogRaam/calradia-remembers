// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerDivorceConsentPolicyTests
   {
      private static PlayerDivorceConsentFacts Happy() => new() {
         SpouseHasPendingDemand = false,
         SpouseRelationWithPlayer = 40
      };

      [Test]
      public void GIVEN_a_happy_spouse_WHEN_the_player_ends_it_THEN_the_split_is_repudiated_not_mutual()
      {
         PlayerDivorceConsentPolicy.SpouseConsents(Happy()).Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_spouse_already_has_a_pending_demand_WHEN_the_player_ends_it_THEN_the_split_is_mutual()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseHasPendingDemand = true;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_spouse_relation_sits_at_the_unhappy_threshold_WHEN_the_player_ends_it_THEN_the_split_is_mutual()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_the_spouse_relation_sits_one_above_the_unhappy_threshold_WHEN_the_player_ends_it_THEN_the_split_is_repudiated()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold + 1;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_deeply_miserable_spouse_WHEN_the_player_ends_it_THEN_the_split_is_mutual()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_THEN_the_split_is_treated_as_repudiated()
      {
         PlayerDivorceConsentPolicy.SpouseConsents(null).Should().BeFalse();
      }
   }
}
