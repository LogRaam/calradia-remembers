// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, Phase 2b: the player's OWN spouse, unprompted, decides whether to raise a divorce demand, entirely
// through conversation (no UI button). ShouldDemand decides WHEN that first demand fires; ShouldEscalate decides
// whether a refused-enough spouse stops asking and BEGINS an estrangement on a deadline despite the player;
// IsReconciled decides whether that estrangement gets called off. Get ShouldDemand wrong and either a happy
// marriage starts pestering the player with unbidden divorce letters, or a miserable one sits mute forever. Get
// the happy-marriage veto wrong (it reads the mod's OWN personal regard, NpcProfile.ReputationWithPlayer, not
// vanilla relation) and a spouse the player has genuinely won over still gets hounded by a stale grudge or a
// lagging vanilla number. Get the pregnant/prisoner gates or the reconciliation hysteresis wrong and a marriage
// ends, or an estrangement is called off, at a moment the story explicitly forbids.

#region

using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NpcMemoryService.Core.Standing;
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

      // The floor the policy stands on: a spouse who is genuinely content must never see a demand fire. Miss
      // this and the weekly evaluation would queue an unbidden "I can no longer bear this marriage" letter
      // against a marriage that was never in trouble.
      [Test]
      public void GIVEN_a_happy_marriage_WHEN_evaluating_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandPolicy.ShouldDemand(Happy()).Should().BeFalse();
      }

      // The core trigger: a marriage this miserable (at/below MiserableRelationThreshold) needs no fresh wound
      // or grudge at all to raise the demand on its own.
      [Test]
      public void GIVEN_a_miserable_relation_WHEN_evaluating_THEN_a_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      // A fresh jealousy wound tips the demand even at a merely moderate relation, ahead of the blended relation
      // ever sinking to MiserableRelationThreshold: the spouse reacts to the wound itself, not to a slow average
      // that may not have caught up yet.
      [Test]
      public void GIVEN_a_fresh_jealousy_wound_WHEN_the_relation_is_only_moderate_THEN_a_demand_is_still_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5; // moderate, not miserable on its own
         facts.RecentJealousyWound = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      // Same fresh-wound logic as jealousy: a just-discovered bastard scandal (HasRecentBastardScandal in the
      // engine layer) is its own trigger, distinct from ordinary marital misery, so it must not wait on the
      // relation number to fall.
      [Test]
      public void GIVEN_a_freshly_discovered_bastard_scandal_WHEN_the_relation_is_only_moderate_THEN_a_demand_is_still_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.DiscoveredBastardScandal = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      // A single grudge heavy enough (at/above GrudgeMagnitudeBar) tips the demand on its own, even over an
      // otherwise merely-moderate relation: one deep wound can outweigh an average that looks civil.
      [Test]
      public void GIVEN_a_live_grudge_above_the_bar_WHEN_the_relation_is_only_moderate_THEN_a_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.LiveGrudgeMagnitudeAgainstPlayer = SpouseDivorceDemandPolicy.GrudgeMagnitudeBar;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      // Pins the boundary one point below GrudgeMagnitudeBar: a grudge just short of the bar, with nothing else
      // in play, must not tip the demand, or the bar would be decorative rather than a real gate.
      [Test]
      public void GIVEN_a_live_grudge_below_the_bar_WHEN_nothing_else_tips_it_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.LiveGrudgeMagnitudeAgainstPlayer = SpouseDivorceDemandPolicy.GrudgeMagnitudeBar - 1;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // Hard gate: even a flatly miserable marriage (-80) never raises a demand during a pregnancy, mirroring
      // the same pregnant/prisoner floor DivorcePolicy already enforces for the NPC-NPC path (Phase 1): a
      // divorce plotline must never collide with a birth.
      [Test]
      public void GIVEN_a_pregnancy_in_play_WHEN_the_marriage_is_miserable_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;
         facts.EitherPregnant = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // Same hard gate for captivity: a demand (and the letter it would queue) must never fire while either
      // party is held prisoner, whatever the relation reads.
      [Test]
      public void GIVEN_either_party_held_prisoner_WHEN_the_marriage_is_miserable_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;
         facts.EitherPrisoner = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // Never re-raise while one demand is already live: without this, a still-miserable marriage would queue a
      // fresh "I can no longer bear this marriage" letter every single weekly tick on top of the one already
      // pending.
      [Test]
      public void GIVEN_a_demand_already_pending_WHEN_the_marriage_is_miserable_THEN_it_is_never_re_raised()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -80;
         facts.AlreadyDemanding = true;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // Fail SAFE toward inaction: a missing lookup must never spuriously fire a demand (and its letter) against
      // the player's marriage. The opposite direction from PlayerDivorceConsentPolicy's null case (which fails
      // toward "repudiated") because here silence should never manufacture a plot beat, not soften one.
      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_THEN_no_demand_is_raised()
      {
         SpouseDivorceDemandPolicy.ShouldDemand(null).Should().BeFalse();
      }

      // ── Happy-marriage veto: high personal regard overrides every other demand trigger ─────────

      // The happy-marriage veto: SpouseReputationWithPlayer is the mod's OWN personal-regard ledger (the number
      // actually built through conversation), source of truth for how the spouse feels, distinct from vanilla
      // relation. A spouse who has genuinely warmed to the player never demands, however the vanilla relation
      // reads.
      [Test]
      public void GIVEN_a_miserable_relation_WHEN_personal_regard_is_high_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // The veto reaches fresh wounds too, not just the blended-relation trigger: a spouse with real personal
      // regard for the player forgives a jealousy wound rather than filing for divorce over it.
      [Test]
      public void GIVEN_a_fresh_jealousy_wound_WHEN_personal_regard_is_high_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.RecentJealousyWound = true;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // And the veto reaches a heavy grudge as well: personal regard overrides even a grudge that alone would
      // otherwise clear GrudgeMagnitudeBar, so a resolved-in-spirit slight cannot resurrect a demand.
      [Test]
      public void GIVEN_a_live_grudge_above_the_bar_WHEN_personal_regard_is_high_THEN_the_demand_is_vetoed()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = -5;
         facts.LiveGrudgeMagnitudeAgainstPlayer = SpouseDivorceDemandPolicy.GrudgeMagnitudeBar;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeFalse();
      }

      // The veto's boundary, one point short: with the veto's own inclusive-threshold test below, this pins that
      // HappyMarriageRegardThreshold is INCLUSIVE, so a retune can never silently blur which side of the line a
      // spouse's regard must clear.
      [Test]
      public void GIVEN_personal_regard_one_below_the_veto_threshold_WHEN_the_marriage_is_miserable_THEN_the_demand_still_fires()
      {
         SpouseDivorceDemandFacts facts = Happy();
         facts.SpouseRelationWithPlayer = SpouseDivorceDemandPolicy.MiserableRelationThreshold;
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold - 1;

         SpouseDivorceDemandPolicy.ShouldDemand(facts).Should().BeTrue();
      }

      // The far side of that same boundary: regard AT the threshold already vetoes. Paired with the test above,
      // this fixes the exact line where a demand goes from firing to vetoed.
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

      // The core escalation trigger: enough refusals (RefusalsBeforeEscalation), with the marriage still
      // unhappy, is what stops the spouse merely asking and starts an estrangement DESPITE the player's
      // continued refusal (the answer to Gabriel's follow-up on a spouse who would otherwise wait forever on a
      // consent that never comes).
      [Test]
      public void GIVEN_enough_refusals_WHEN_the_marriage_is_still_unhappy_THEN_the_spouse_escalates()
      {
         SpouseDivorceDemandPolicy.ShouldEscalate(HappyEscalation()).Should().BeTrue();
      }

      // Pins the boundary one refusal short of the bar: escalation must not fire early, or a spouse would begin
      // an estrangement before the player has had a fair chance to be asked again.
      [Test]
      public void GIVEN_one_fewer_refusal_than_the_bar_WHEN_the_marriage_is_still_unhappy_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.RefusalCount = SpouseDivorceDemandPolicy.RefusalsBeforeEscalation - 1;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // Even with enough refusals banked, a relation that has genuinely climbed back out of misery
      // (DivorcePolicy.UnhappyThreshold, the same "unhappy" bar the rest of the divorce feature shares) must
      // stop the escalation: the refusal count alone must never override an actually-mended marriage.
      [Test]
      public void GIVEN_enough_refusals_WHEN_the_relation_has_since_recovered_above_the_misery_bar_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseRelationWithPlayer = DivorcePolicy.UnhappyThreshold + 1;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // Guards against an off-by-one that only matched the bar exactly: refusals well past the bar must still
      // escalate, not require landing on RefusalsBeforeEscalation precisely.
      [Test]
      public void GIVEN_more_than_enough_refusals_WHEN_the_marriage_is_still_unhappy_THEN_the_spouse_escalates()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.RefusalCount = SpouseDivorceDemandPolicy.RefusalsBeforeEscalation + 5;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeTrue();
      }

      // Never begin a second estrangement over an active one: without this, RegisterRefusal on top of an
      // already-estranging spouse would reset the deadline (BeginEstrangement) and requeue the announcing
      // letter every time.
      [Test]
      public void GIVEN_an_estrangement_already_under_way_WHEN_evaluating_THEN_it_is_never_begun_twice()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.AlreadyEscalating = true;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // Same hard pregnant gate as ShouldDemand: an estrangement (and the walk-out it leads to) must never begin
      // during a pregnancy, whatever the refusal count.
      [Test]
      public void GIVEN_a_pregnancy_in_play_WHEN_the_refusal_bar_is_cleared_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.EitherPregnant = true;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // Same hard captivity gate: an estrangement must never begin while either party is held prisoner.
      [Test]
      public void GIVEN_either_party_held_prisoner_WHEN_the_refusal_bar_is_cleared_THEN_no_escalation()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.EitherPrisoner = true;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // Fail SAFE toward inaction, same reasoning as ShouldDemand's null case: a missing lookup must never
      // silently begin an estrangement.
      [Test]
      public void GIVEN_null_facts_WHEN_evaluating_escalation_THEN_no_escalation_is_raised()
      {
         SpouseDivorceDemandPolicy.ShouldEscalate(null).Should().BeFalse();
      }

      // ── Happy-marriage veto: high personal regard overrides escalation too ──────────────────────

      // Same happy-marriage veto as ShouldDemand: a spouse who has genuinely warmed to the player, in personal
      // regard, never escalates either, however many refusals piled up before that warming happened.
      [Test]
      public void GIVEN_enough_refusals_WHEN_personal_regard_is_high_THEN_the_escalation_is_vetoed()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // Pins the same veto boundary for escalation as the ShouldDemand tests above: one point short of
      // HappyMarriageRegardThreshold, the veto does not yet apply.
      [Test]
      public void GIVEN_personal_regard_one_below_the_veto_threshold_WHEN_the_refusal_bar_is_cleared_THEN_the_escalation_still_fires()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold - 1;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeTrue();
      }

      // The far side: regard AT the threshold already vetoes escalation too, fixing the exact line alongside
      // the ShouldDemand boundary tests above.
      [Test]
      public void GIVEN_personal_regard_at_the_veto_threshold_WHEN_the_refusal_bar_is_cleared_THEN_the_escalation_is_vetoed()
      {
         SpouseDivorceEscalationFacts facts = HappyEscalation();
         facts.SpouseReputationWithPlayer = SpouseDivorceDemandPolicy.HappyMarriageRegardThreshold;

         SpouseDivorceDemandPolicy.ShouldEscalate(facts).Should().BeFalse();
      }

      // ── Reconciliation: hysteresis above the misery bar calls an active estrangement off ────────

      // The reconciliation trigger itself: relation AT ReconciliationThreshold already calls the active
      // estrangement off.
      [Test]
      public void GIVEN_relation_at_the_reconciliation_threshold_WHEN_checking_reconciliation_THEN_it_is_reconciled()
      {
         SpouseDivorceDemandPolicy.IsReconciled(new Regard(SpouseDivorceDemandPolicy.ReconciliationThreshold)).Should().BeTrue();
      }

      // Pins the boundary one point short: an estrangement must keep running until the relation genuinely
      // clears the threshold, not merely approaches it.
      [Test]
      public void GIVEN_relation_one_below_the_reconciliation_threshold_WHEN_checking_reconciliation_THEN_it_is_not_reconciled()
      {
         SpouseDivorceDemandPolicy.IsReconciled(new Regard(SpouseDivorceDemandPolicy.ReconciliationThreshold - 1)).Should().BeFalse();
      }

      // Without this hysteresis gap, a spouse who merely stops being counted "unhappy" (crossing
      // DivorcePolicy.UnhappyThreshold, not ReconciliationThreshold) would flip the estrangement off and on
      // again on every minor tick near the misery bar, rather than only on a genuine, sustained warming.
      [Test]
      public void GIVEN_relation_merely_clearing_the_misery_bar_WHEN_checking_reconciliation_THEN_it_is_not_reconciled()
      {
         // The hysteresis gap itself: recovering just past DivorcePolicy.UnhappyThreshold (no longer
         // "unhappy") is deliberately NOT enough on its own to count as reconciled.
         SpouseDivorceDemandPolicy.IsReconciled(new Regard(DivorcePolicy.UnhappyThreshold + 1)).Should().BeFalse();
      }
   }
}
