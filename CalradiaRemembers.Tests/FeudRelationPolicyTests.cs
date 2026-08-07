// Code written by Gabriel Mailhot, 07/08/2026.
// FeudRelationPolicy decides what a resolved lord-vs-lord scheme does to REAL vanilla relations: how hard the
// plotter/target bond sours, and how much the target's close allies cool toward the plotter. That relation
// drop is the WHOLE lever of the Living World pillar's Inc 1: vanilla DefaultDiplomacyModel.GetScoreOfDeclaringWar
// includes a relationScore term and the kingdom AI proposes/votes war when the score clears its threshold, so a
// feud that HOLDS lets war EMERGE on its own. The mod never decrees war; it only worsens relations here. These
// tests pin the delta DECISIONS only. They deliberately assert NOTHING about war being declared: that is vanilla's
// call, downstream of the number this policy returns. Wrong magnitudes either make feuds inert noise (repeated
// plots never build toward the war threshold) or let one plot detonate a war this feature has no business forcing;
// a wrong "which outcome is a feud" boundary punishes a plot nobody ever knew about, or double-counts one that did.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class FeudRelationPolicyTests
   {
      // A landed slander is the deepest single-feud rupture the policy produces: the plot actually harmed the
      // target AND slander is a personal, public affront to the name, so it earns the slander bonus on top of the
      // succeeded base. This is the top of the scale the other outcomes are judged against; get it wrong and the
      // strongest feud either never bites hard enough to feed vanilla's war score, or bites so hard one plot alone
      // could tip a kingdom into war.
      [Test]
      public void GIVEN_a_slander_that_landed_WHEN_resolving_THEN_the_bond_sours_hardest()
      {
         FeudRelationOutcome outcome = FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Succeeded);

         outcome.CreatesFeud.Should().BeTrue();
         outcome.PlotterTargetDelta.Should().Be(-(FeudRelationPolicy.SucceededBasePenalty + FeudRelationPolicy.SlanderRelationBonus));
      }

      // Sabotage that landed uses the succeeded base with NO slander bonus: its bite is against standing/influence
      // (which SchemeStore already docks separately as a clan-influence hit), so the relation rupture must be a
      // touch lighter than slander's. Pins that the two kinds are distinct signals, not conflated.
      [Test]
      public void GIVEN_a_sabotage_that_landed_WHEN_resolving_THEN_the_bond_sours_by_the_base_without_the_slander_bonus()
      {
         FeudRelationOutcome outcome = FeudRelationPolicy.Resolve(SchemeKind.Sabotage, SchemeState.Succeeded);

         outcome.PlotterTargetDelta.Should().Be(-FeudRelationPolicy.SucceededBasePenalty);
      }

      // A plot merely CAUGHT (Discovered) is a real feud but must rupture the bond LESS than one that actually
      // landed: the target now knows a rival moved against them, but the harm never completed. If discovered were
      // to match or exceed succeeded, catching a plot would be as bad as suffering it, and a plotter would have no
      // reason to fear the plot working over being exposed.
      [Test]
      public void GIVEN_a_caught_plot_WHEN_resolving_THEN_the_rupture_is_smaller_than_a_landed_one()
      {
         int caught = -FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Discovered).PlotterTargetDelta;
         int landed = -FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Succeeded).PlotterTargetDelta;

         caught.Should().BeLessThan(landed);
         caught.Should().Be(FeudRelationPolicy.DiscoveredBasePenalty + FeudRelationPolicy.SlanderRelationBonus);
      }

      // At the same outcome, slander must sour the bond itself harder than sabotage: slander blackens the name
      // (relational damage), sabotage undermines standing (covered by the influence hit elsewhere). Guards the
      // kind bonus from being silently dropped, which would flatten the two schemes into one.
      [Test]
      public void GIVEN_the_same_outcome_WHEN_comparing_kinds_THEN_slander_bites_the_bond_harder_than_sabotage()
      {
         int slander = -FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Succeeded).PlotterTargetDelta;
         int sabotage = -FeudRelationPolicy.Resolve(SchemeKind.Sabotage, SchemeState.Succeeded).PlotterTargetDelta;

         slander.Should().BeGreaterThan(sabotage);
      }

      // The whole point of cooling the target's ALLIES is to make the feud visible to third parties so they "learn
      // and adjust", but they are bystanders, not the wronged party, so the hit on each must stay a modest fraction
      // of the principals' rupture. Pins ally cooling to the documented divisor of the plotter/target delta.
      [Test]
      public void GIVEN_a_landed_feud_WHEN_reading_ally_cooling_THEN_it_is_a_bounded_fraction_of_the_principal_rupture()
      {
         FeudRelationOutcome outcome = FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Succeeded);
         int principal = -outcome.PlotterTargetDelta;

         (-outcome.AllyCoolingDelta).Should().Be(principal / FeudRelationPolicy.AllyCoolingDivisor);
         (-outcome.AllyCoolingDelta).Should().BeLessThan(principal);
      }

      // The floor guarantees a fan-out is never a silent no-op: even the smallest feud (a caught sabotage) must cool
      // allies by at least MinAllyCooling, or the "third parties adjust" behaviour would vanish exactly where the
      // rupture is smallest, and the feud would be invisible to everyone but the two principals.
      [Test]
      public void GIVEN_the_smallest_feud_WHEN_reading_ally_cooling_THEN_it_never_falls_below_the_floor()
      {
         FeudRelationOutcome outcome = FeudRelationPolicy.Resolve(SchemeKind.Sabotage, SchemeState.Discovered);

         (-outcome.AllyCoolingDelta).Should().Be(FeudRelationPolicy.MinAllyCooling);
      }

      // A landed plot must reach a WIDER circle of the target's allies than a merely caught one: a completed harm is
      // spoken of further than a foiled attempt. Pins the two fan-out caps so a retune keeps succeeded >= discovered
      // and neither is unbounded (the fan-out is meant to be modest, never the whole realm).
      [Test]
      public void GIVEN_a_landed_versus_a_caught_feud_WHEN_reading_fan_out_THEN_the_landed_one_reaches_wider()
      {
         int landed = FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Succeeded).AllyFanOutCap;
         int caught = FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Discovered).AllyFanOutCap;

         landed.Should().Be(FeudRelationPolicy.SucceededFanOutCap);
         caught.Should().Be(FeudRelationPolicy.DiscoveredFanOutCap);
         landed.Should().BeGreaterThan(caught);
      }

      // A plot that Failed fizzled UNSEEN (SchemeBacklash's own model treats Failed as "without exposure"): nobody
      // learned of it, so there is no visible feud and nothing must move. Get this wrong and two lords sour toward
      // each other over a plot neither the target nor the realm ever knew happened, feeding a war out of nothing.
      [Test]
      public void GIVEN_a_plot_that_fizzled_unseen_WHEN_resolving_THEN_there_is_no_feud_and_nothing_moves()
      {
         FeudRelationOutcome outcome = FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Failed);

         outcome.CreatesFeud.Should().BeFalse();
         outcome.PlotterTargetDelta.Should().Be(0);
         outcome.AllyCoolingDelta.Should().Be(0);
         outcome.AllyFanOutCap.Should().Be(0);
      }

      // A scheme still in motion (Plotted/Progressing) has not resolved: applying a feud now would move relations for
      // a plot that may never land or be caught. The feud must fire only at a genuine resolution, so these states
      // return None and change nothing.
      [Test]
      public void GIVEN_a_scheme_still_in_motion_WHEN_resolving_THEN_no_feud_is_created_yet()
      {
         FeudRelationPolicy.Resolve(SchemeKind.Slander, SchemeState.Plotted).CreatesFeud.Should().BeFalse();
         FeudRelationPolicy.Resolve(SchemeKind.Sabotage, SchemeState.Progressing).CreatesFeud.Should().BeFalse();
      }
   }
}
