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
         //Arrange — an oath-breaker's name should cost more than a kept vow earns; asymmetric stakes.
         Math.Abs(CommitmentPolicy.BreachedRelationPenalty).Should().BeGreaterThan(CommitmentPolicy.FulfilledRelationBonus);
      }
   }
}
