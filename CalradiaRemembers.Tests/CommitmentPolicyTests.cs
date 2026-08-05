// Code written by Gabriel Mailhot, 02/07/2026.
// Pure math behind a TrackedCommitment (a lord's PLEDGE to move against a rival): when its deadline falls,
// and how much it moves relation/stance when honored versus broken. CommitmentBehavior applies these numbers
// straight to the player's regard with the lord (StanceService.OnPromiseKept / OnPromiseBroken) and to the
// daily overdue sweep that decides whether a pledge is still alive, so a wrong deadline either lets a broken
// vow slide forever or breaches a pledge the lord hasn't had time to keep, and a wrong magnitude either lets
// an empty promise cost nothing or rewards a kept one more than breaking one stings.

#region

using System;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CommitmentPolicyTests
   {
      // ── deadline math ────────────────────────────────────────────────────

      // Pins the "roughly one season" tuning (DeadlineDays); a silent retune here would quietly change how
      // long a lord has to make good on a pledge without anyone noticing.
      [Test]
      public void GIVEN_an_issued_day_WHEN_computing_the_deadline_THEN_it_is_one_season_later()
      {
         CommitmentPolicy.ComputeDeadline(100).Should().Be(100 + CommitmentPolicy.DeadlineDays);
      }

      // The zero-day edge: guards against an off-by-one that would give a pledge issued on day zero either
      // no grace period at all or an extra one.
      [Test]
      public void GIVEN_an_issued_day_of_zero_WHEN_computing_the_deadline_THEN_it_is_just_the_season_length()
      {
         CommitmentPolicy.ComputeDeadline(0).Should().Be(CommitmentPolicy.DeadlineDays);
      }

      // ── overdue ──────────────────────────────────────────────────────────

      // A pledge still within its grace period must never trip the daily overdue sweep and get wrongly
      // breached under the lord.
      [Test]
      public void GIVEN_the_deadline_not_yet_reached_WHEN_checking_overdue_THEN_it_is_not_overdue()
      {
         CommitmentPolicy.IsOverdue(100, 121).Should().BeFalse();
      }

      // The threshold is deliberately INCLUSIVE: a lord who has had exactly their full season, not one day
      // less, is breached the day the deadline lands, not the day after.
      [Test]
      public void GIVEN_the_deadline_reached_exactly_WHEN_checking_overdue_THEN_the_threshold_is_inclusive()
      {
         CommitmentPolicy.IsOverdue(121, 121).Should().BeTrue();
      }

      // Confirms the comparison holds well past the boundary too, not merely exactly at it.
      [Test]
      public void GIVEN_well_past_the_deadline_WHEN_checking_overdue_THEN_it_is_overdue()
      {
         CommitmentPolicy.IsOverdue(200, 121).Should().BeTrue();
      }

      // ── consequence magnitudes ───────────────────────────────────────────

      // A kept pledge must always reward the lord's regard for the player, never accidentally read as
      // a penalty through a sign error.
      [Test]
      public void GIVEN_the_fulfilled_bonus_WHEN_read_THEN_it_is_a_positive_reward()
      {
         CommitmentPolicy.FulfilledRelationBonus.Should().BePositive();
      }

      // A broken pledge must always cost the lord's regard, matching StanceService.OnPromiseBroken's
      // "real trust crater", never a sign error that rewards an unkept vow.
      [Test]
      public void GIVEN_the_breached_penalty_WHEN_read_THEN_it_is_a_negative_penalty()
      {
         CommitmentPolicy.BreachedRelationPenalty.Should().BeNegative();
      }

      [Test]
      public void GIVEN_the_breach_and_fulfillment_magnitudes_WHEN_compared_THEN_breaking_a_promise_stings_harder_than_keeping_one_rewards()
      {
         //Arrange, an oath-breaker's name should cost more than a kept vow earns; asymmetric stakes.
         Math.Abs(CommitmentPolicy.BreachedRelationPenalty).Should().BeGreaterThan(CommitmentPolicy.FulfilledRelationBonus);
      }

      // ── R7-light: OnOverdue (KeepPeace's own inversion) ──────────────────

      // STAKE: MoveAgainst, PayGold, and Protect all need a POSITIVE act before the deadline (move against the
      // rival, pay the sum, fight at the player's side); reaching the deadline with none is a broken oath. If
      // this ever flipped to Fulfill by mistake, CommitmentBehavior would reward a lord who did nothing at all.
      [TestCase(CommitmentKind.MoveAgainst)]
      [TestCase(CommitmentKind.PayGold)]
      [TestCase(CommitmentKind.Protect)]
      public void GIVEN_a_kind_needing_a_positive_act_WHEN_the_deadline_lapses_with_nothing_verified_THEN_it_is_a_breach(CommitmentKind kind)
      {
         CommitmentPolicy.OnOverdue(kind).Should().Be(OverdueOutcome.Breach);
      }

      // STAKE: KeepPeace is the sole inversion (COUNCIL_ACTIONS.md's own framing): the vow is "we will NOT go
      // to war", so silence at the deadline IS the oath honored. A war is caught immediately by
      // CommitmentBehavior.OnWarDeclared, never by this sweep, so reaching the deadline unbroken must always
      // read as Fulfill; getting this backwards would punish a lord who genuinely kept the peace.
      [Test]
      public void GIVEN_keep_peace_WHEN_the_deadline_lapses_with_no_war_declared_THEN_it_is_a_fulfillment()
      {
         CommitmentPolicy.OnOverdue(CommitmentKind.KeepPeace).Should().Be(OverdueOutcome.Fulfill);
      }

      // ── R7-light: the public-oath witness cost ───────────────────────────

      // STAKE (COUNCIL_ACTIONS.md's swear_oath section): a private oath (no witnesses recorded, e.g. a 1:1
      // pledge_against) must cost NOTHING extra beyond the base BreachedRelationPenalty: the witness cost is a
      // COUNCIL-only sanction, never applied where nobody actually heard the vow.
      [Test]
      public void GIVEN_zero_witnesses_WHEN_computing_the_public_oath_penalty_THEN_it_adds_nothing()
      {
         CommitmentPolicy.WitnessBreachPenalty(0).Should().Be(0);
      }

      // STAKE: each witness costs PerWitnessBreachPenalty, so a small table's own breach cost scales with how
      // publicly it was sworn, exactly the "coute plus cher devant temoins" rule this whole feature exists for.
      [Test]
      public void GIVEN_one_witness_WHEN_computing_the_public_oath_penalty_THEN_it_matches_the_per_witness_rate()
      {
         CommitmentPolicy.WitnessBreachPenalty(1).Should().Be(CommitmentPolicy.PerWitnessBreachPenalty);
      }

      // STAKE: the total per-witness cost must never exceed MaxWitnessBreachPenalty however many members sat at
      // a packed WarCouncil table, or one broken oath could turn into an unbounded relation catastrophe.
      [Test]
      public void GIVEN_enough_witnesses_to_exceed_the_floor_WHEN_computing_the_public_oath_penalty_THEN_it_is_capped()
      {
         int uncappedIfLinear = CommitmentPolicy.PerWitnessBreachPenalty * 10;
         uncappedIfLinear.Should().BeLessThan(CommitmentPolicy.MaxWitnessBreachPenalty); // sanity: 10 witnesses would otherwise blow past the floor

         CommitmentPolicy.WitnessBreachPenalty(10).Should().Be(CommitmentPolicy.MaxWitnessBreachPenalty);
      }

      // A negative witness count (defensive: should never happen, but a caller passing a raw list Count could
      // theoretically underflow) must never invert into a reward.
      [Test]
      public void GIVEN_a_negative_witness_count_WHEN_computing_the_public_oath_penalty_THEN_it_adds_nothing()
      {
         CommitmentPolicy.WitnessBreachPenalty(-1).Should().Be(0);
      }

      // ── R7-light: PayGold's promised-amount sanity clamp ─────────────────

      // STAKE: a promised sum must never exceed give_gold's own council ceiling (GoldPledgePolicy.MaxPayable):
      // an oath is not a licence to promise a figure the mod would never let an instant pledge move either.
      [Test]
      public void GIVEN_an_amount_over_the_ceiling_WHEN_clamped_THEN_it_is_capped_to_the_gold_pledge_ceiling()
      {
         CommitmentPolicy.ClampPromisedAmount(999999).Should().Be(CalradiaRemembers.Logic.Assemblies.GoldPledgePolicy.MaxPayable);
      }

      // An ordinary, reasonable sum must pass through unchanged: the clamp exists to catch runaway or
      // hallucinated figures, not to second-guess a sane pledge.
      [Test]
      public void GIVEN_an_ordinary_amount_WHEN_clamped_THEN_it_passes_through_unchanged()
      {
         CommitmentPolicy.ClampPromisedAmount(300).Should().Be(300);
      }

      // STAKE: a non-positive or unparsed amount must never become a "0 denars owed" oath that fulfills itself
      // the instant it is sworn (OnGoldGivenToPlayer would treat 0 as already paid): CouncilLift.SettleSwearOath
      // reads a zero-or-less clamp result as an outright refusal instead.
      [TestCase(0)]
      [TestCase(-50)]
      public void GIVEN_a_non_positive_amount_WHEN_clamped_THEN_it_resolves_to_zero(int proposed)
      {
         CommitmentPolicy.ClampPromisedAmount(proposed).Should().Be(0);
      }

      // ── R7-light: tuning sanity ───────────────────────────────────────────

      // Mirrors the existing sign-error guard on BreachedRelationPenalty/FulfilledRelationBonus: a witness
      // penalty that was accidentally positive would REWARD a public betrayal instead of punishing it.
      [Test]
      public void GIVEN_the_per_witness_penalty_WHEN_read_THEN_it_is_negative()
      {
         CommitmentPolicy.PerWitnessBreachPenalty.Should().BeNegative();
      }

      // The BrokenOath grudge magnitude must be a real, planted magnitude (positive), never zero or negative,
      // or GrudgeBehavior.TryPlant's own ShouldForm gate would refuse to seat it at all.
      [Test]
      public void GIVEN_the_broken_oath_grudge_magnitude_WHEN_read_THEN_it_is_positive()
      {
         CommitmentPolicy.BrokenOathGrudgeMagnitude.Should().BePositive();
      }
   }
}
