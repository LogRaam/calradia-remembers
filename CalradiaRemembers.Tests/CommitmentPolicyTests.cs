// Code written by Gabriel Mailhot, 02/07/2026.

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

      [Test]
      public void GIVEN_an_issued_day_WHEN_computing_the_deadline_THEN_it_is_one_season_later()
      {
         CommitmentPolicy.ComputeDeadline(100).Should().Be(100 + CommitmentPolicy.DeadlineDays);
      }

      [Test]
      public void GIVEN_an_issued_day_of_zero_WHEN_computing_the_deadline_THEN_it_is_just_the_season_length()
      {
         CommitmentPolicy.ComputeDeadline(0).Should().Be(CommitmentPolicy.DeadlineDays);
      }

      // ── overdue ──────────────────────────────────────────────────────────

      [Test]
      public void GIVEN_the_deadline_not_yet_reached_WHEN_checking_overdue_THEN_it_is_not_overdue()
      {
         CommitmentPolicy.IsOverdue(100, 121).Should().BeFalse();
      }

      [Test]
      public void GIVEN_the_deadline_reached_exactly_WHEN_checking_overdue_THEN_the_threshold_is_inclusive()
      {
         CommitmentPolicy.IsOverdue(121, 121).Should().BeTrue();
      }

      [Test]
      public void GIVEN_well_past_the_deadline_WHEN_checking_overdue_THEN_it_is_overdue()
      {
         CommitmentPolicy.IsOverdue(200, 121).Should().BeTrue();
      }

      // ── consequence magnitudes ───────────────────────────────────────────

      [Test]
      public void GIVEN_the_fulfilled_bonus_WHEN_read_THEN_it_is_a_positive_reward()
      {
         CommitmentPolicy.FulfilledRelationBonus.Should().BePositive();
      }

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
