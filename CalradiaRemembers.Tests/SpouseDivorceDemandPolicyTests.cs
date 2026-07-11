// Code written by Gabriel Mailhot, 04/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SpouseDivorceDemandPolicyTests
   {
      private static SpouseDivorceDemandFacts Happy() => new() {
         SpouseRelationWithPlayer = 40,
         SpouseReputationWithPlayer = 0, // below HappyMarriageRegardThreshold: does not itself veto these scenarios
         RecentJealousyWound = false,
         DiscoveredBastardScandal = false,
         LiveGrudgeMagnitudeAgainstPlayer = 0,
         AlreadyDemanding = false,
         EitherPregnant = false,
         EitherPrisoner = false
      };

      [Test]
      public void GIVEN_a_happy_marriage_WHEN_evaluating_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandPolicy.ShouldDemand(Happy()).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_miserable_relation_WHEN_evaluating_THEN_a_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_fresh_jealousy_wound_WHEN_the_relation_is_only_moderate_THEN_a_demand_is_still_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5; // moderate, not miserable on its own
         facts.RecentJealousyWound = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_freshly_discovered_bastard_scandal_WHEN_the_relation_is_only_moderate_THEN_a_demand_is_still_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.DiscoveredBastardScandal = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_live_grudge_above_the_bar_WHEN_the_relation_is_only_moderate_THEN_a_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.LiveGrudgeMagnitudeAgainstPlayer = SpouseDivorceDemandPolicy.GrudgeMagnitudeBar;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_live_grudge_below_the_bar_WHEN_nothing_else_tips_it_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.LiveGrudgeMagnitudeAgainstPlayer = SpouseDivorceDemandPolicy.GrudgeMagnitudeBar - 1;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_pregnancy_in_play_WHEN_the_marriage_is_miserable_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;
         facts.EitherPregnant = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_either_party_held_prisoner_WHEN_the_marriage_is_miserable_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;
         facts.EitherPrisoner = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_demand_already_pending_WHEN_the_marriage_is_miserable_THEN_it_is_never_re_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;
         facts.AlreadyDemanding = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandPolicy.ShouldDemand(null).Should().BeFalse();
      }

      // ── Happy-marriage veto: high personal regard overrides every other demand trigger ─────────

      [Test]
      public void GIVEN_a_miserable_relation_WHEN_personal_regard_is_high_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_fresh_jealousy_wound_WHEN_personal_regard_is_high_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.RecentJealousyWound = true;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_live_grudge_above_the_bar_WHEN_personal_regard_is_high_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.LiveGrudgeMagnitudeAgainstPlayer = SpouseDivorceDemandPolicy.GrudgeMagnitudeBar;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_personal_regard_one_below_the_veto_threshold_WHEN_the_marriage_is_miserable_THEN_the_demand_still_fires()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold - 1;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_personal_regard_at_the_veto_threshold_WHEN_the_marriage_is_miserable_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // ── Escalation: a refused-enough, still-miserable spouse begins ending it themselves ──────────

      private static SpouseDivorceEscalationFacts HappyEscalation() => new() {
         RefusalCount = SpouseDivorceDemandPolicy.RefusalsBeforeEscalation,
         SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold,
         SpouseReputationWithPlayer = 0, // below HappyMarriageRegardThreshold: does not itself veto these scenarios
         AlreadyEscalating = false,
         EitherPregnant = false,
         EitherPrisoner = false
      };

      [Test]
      public void GIVEN_enough_refusals_WHEN_the_marriage_is_still_unhappy_THEN_the_spouse_escalates()
      {
         SpouseDivorceDemandPolicy.ShouldEscalate(HappyEscalation()).Should().BeTrue();
      }

      [Test]
      public void GIVEN_one_fewer_refusal_than_the_bar_WHEN_the_marriage_is_still_unhappy_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.RefusalCount = SpouseDivorceDemandPolicy.RefusalsBeforeEscalation - 1;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_enough_refusals_WHEN_the_relation_has_since_recovered_above_the_misery_bar_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold + 1;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_more_than_enough_refusals_WHEN_the_marriage_is_still_unhappy_THEN_the_spouse_escalates()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.RefusalCount = SpouseDivorceDemandPolicy.RefusalsBeforeEscalation + 5;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_estrangement_already_under_way_WHEN_evaluating_THEN_it_is_never_begun_twice()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.AlreadyEscalating = true;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_a_pregnancy_in_play_WHEN_the_refusal_bar_is_cleared_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.EitherPregnant = true;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_either_party_held_prisoner_WHEN_the_refusal_bar_is_cleared_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.EitherPrisoner = true;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_escalation_THEN_no_escalation_is_raised()
      {
         SpouseDivorceDemandPolicy.ShouldEscalate(null).Should().BeFalse();
      }

      // ── Happy-marriage veto: high personal regard overrides escalation too ──────────────────────

      [Test]
      public void GIVEN_enough_refusals_WHEN_personal_regard_is_high_THEN_the_escalation_is_vetoed()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      [Test]
      public void GIVEN_personal_regard_one_below_the_veto_threshold_WHEN_the_refusal_bar_is_cleared_THEN_the_escalation_still_fires()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold - 1;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeTrue();
      }

      [Test]
      public void GIVEN_personal_regard_at_the_veto_threshold_WHEN_the_refusal_bar_is_cleared_THEN_the_escalation_is_vetoed()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // ── Reconciliation: hysteresis above the misery bar calls an active estrangement off ────────

      [Test]
      public void GIVEN_relation_at_the_reconciliation_threshold_WHEN_checking_reconciliation_THEN_it_is_reconciled()
      {
         SpouseDivorceDemandPolicy.IsReconciled(SpouseDivorceDemandPolicy.ReconciliationThreshold).Should().BeTrue();
      }

      [Test]
      public void GIVEN_relation_one_below_the_reconciliation_threshold_WHEN_checking_reconciliation_THEN_it_is_not_reconciled()
      {
         SpouseDivorceDemandPolicy.IsReconciled(SpouseDivorceDemandPolicy.ReconciliationThreshold - 1).Should().BeFalse();
      }

      [Test]
      public void GIVEN_relation_merely_clearing_the_misery_bar_WHEN_checking_reconciliation_THEN_it_is_not_reconciled()
      {
         // The hysteresis gap itself: recovering just past DivorcePolicy.UnhappyThreshold (no longer
         // "unhappy") is deliberately NOT enough on its own to count as reconciled.
         SpouseDivorceDemandPolicy.IsReconciled(DivorcePolicy.UnhappyThreshold + 1).Should().BeFalse();
      }
   }
}
