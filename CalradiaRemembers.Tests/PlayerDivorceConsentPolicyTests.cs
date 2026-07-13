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
      // Well past the bar, not merely sitting on it: a spouse who loathes the player is RELEASED, not repudiated.
      // This guards against a policy that only ever compared for equality with the threshold instead of ordering,
      // which would have blamed the player for freeing someone who despised them.
      [Test]
      public void GIVEN_a_deeply_miserable_spouse_WHEN_the_player_ends_it_THEN_the_split_is_mutual()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeTrue();
      }

      // The floor the whole policy stands on: a spouse who is happy with you plainly did NOT want this. Setting
      // them aside has to cost the player the full repudiation, or ending a good marriage would be free and the
      // tiered consequence engine would have nothing left to judge.
      [Test]
      public void GIVEN_a_happy_spouse_WHEN_the_player_ends_it_THEN_the_split_is_repudiated_not_mutual()
      {
         PlayerDivorceConsentPolicy.SpouseConsents(Happy()).Should().BeFalse();
      }

      // Fail SAFE, never silently lenient. With no facts to judge on, assume the spouse did NOT consent: a
      // missing lookup or a bad state must never quietly become a free, consequence-free divorce, which is the
      // failure the player would never notice and never report.
      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_THEN_the_split_is_treated_as_repudiated()
      {
         PlayerDivorceConsentPolicy.SpouseConsents(null).Should().BeFalse();
      }

      // The spouse is ALREADY demanding a divorce of their own (Phase 2b). A pending demand is the clearest
      // signal a spouse can give that they want out, so the player who ends it is granting a wish, not
      // repudiating anyone. Get this one wrong and the player eats the whole grudge storm (the spouse, their
      // family, and their three best friends) for ending a marriage the spouse was itself petitioning to end.
      [Test]
      public void GIVEN_the_spouse_already_has_a_pending_demand_WHEN_the_player_ends_it_THEN_the_split_is_mutual()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseHasPendingDemand = true;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeTrue();
      }

      // The boundary, and it must be INCLUSIVE. The bar is DivorcePolicy.UnhappyThreshold, deliberately the very
      // same one the NPC-NPC case uses to judge an unhappy spouse, so a single marriage can never be read as
      // unhappy by one system and happy by another. Pinning the exact threshold keeps that shared bar honest if
      // it is ever retuned.
      [Test]
      public void GIVEN_the_spouse_relation_sits_at_the_unhappy_threshold_WHEN_the_player_ends_it_THEN_the_split_is_mutual()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeTrue();
      }

      // The far side of that same boundary: one point above the bar and the spouse was not unhappy enough to
      // have wanted out, so the split IS a repudiation. Paired with the test above, this fixes the line exactly,
      // so a future retune of UnhappyThreshold can never silently move who gets blamed for the marriage ending.
      [Test]
      public void GIVEN_the_spouse_relation_sits_one_above_the_unhappy_threshold_WHEN_the_player_ends_it_THEN_the_split_is_repudiated()
      {
         PlayerDivorceConsentFacts facts = Happy();
         facts.SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold + 1;

         PlayerDivorceConsentPolicy.SpouseConsents(facts).Should().BeFalse();
      }

      #region private

      private static PlayerDivorceConsentFacts Happy(int relationWithPlayer = 40) => new() {
         SpouseHasPendingDemand = false,
         SpouseRelationWithPlayer = relationWithPlayer
      };

      #endregion
   }
}