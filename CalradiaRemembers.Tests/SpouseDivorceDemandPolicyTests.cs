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
   }
}
