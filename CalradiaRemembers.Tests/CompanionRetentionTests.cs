// Code written by Gabriel Mailhot, 28/06/2026.
// Protects the companion-retention loop: an unhappy companion asks for an audience, a granted audience clears the
// grievance, refusals accumulate, and after the cap the companion leaves by farewell — never silently.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionRetentionTests
   {
      // ── The toggle / not-unhappy guard ─────────────────────────────────────

      [Test]
      public void GIVEN_contentment_disabled_WHEN_decided_THEN_nothing_happens_even_if_unhappy()
      {
         CompanionRetentionPolicy.Decide(false, true, 0).Should().Be(RetentionStep.None);
         CompanionRetentionPolicy.Decide(false, true, CompanionRetentionPolicy.MaxAudienceRefusals).Should().Be(RetentionStep.None);
      }

      [Test]
      public void GIVEN_a_content_companion_WHEN_decided_THEN_nothing_happens()
         => CompanionRetentionPolicy.Decide(true, false, 0).Should().Be(RetentionStep.None);

      // ── Audience requests ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_an_unhappy_companion_below_the_cap_WHEN_decided_THEN_they_ask_for_an_audience()
      {
         CompanionRetentionPolicy.Decide(true, true, 0).Should().Be(RetentionStep.RequestAudience);
         CompanionRetentionPolicy.Decide(true, true, CompanionRetentionPolicy.MaxAudienceRefusals - 1)
            .Should().Be(RetentionStep.RequestAudience);
      }

      // ── Patience runs out ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_the_refusal_cap_is_reached_WHEN_decided_THEN_they_send_a_farewell()
         => CompanionRetentionPolicy.Decide(true, true, CompanionRetentionPolicy.MaxAudienceRefusals)
            .Should().Be(RetentionStep.SendFarewell);

      // ── Refusal accumulation ───────────────────────────────────────────────

      [Test]
      public void GIVEN_a_refused_audience_WHEN_registered_THEN_the_count_rises_by_one()
      {
         CompanionRetentionPolicy.RegisterRefusal(0).Should().Be(1);
         CompanionRetentionPolicy.RegisterRefusal(2).Should().Be(3);
      }

      [Test]
      public void GIVEN_three_refusals_in_a_row_WHEN_decided_THEN_only_the_fourth_decide_leaves()
      {
         var r = 0;
         CompanionRetentionPolicy.Decide(true, true, r).Should().Be(RetentionStep.RequestAudience);
         r = CompanionRetentionPolicy.RegisterRefusal(r); // 1
         CompanionRetentionPolicy.Decide(true, true, r).Should().Be(RetentionStep.RequestAudience);
         r = CompanionRetentionPolicy.RegisterRefusal(r); // 2
         CompanionRetentionPolicy.Decide(true, true, r).Should().Be(RetentionStep.RequestAudience);
         r = CompanionRetentionPolicy.RegisterRefusal(r); // 3
         CompanionRetentionPolicy.Decide(true, true, r).Should().Be(RetentionStep.SendFarewell);
      }

      // ── A granted audience mollifies them ──────────────────────────────────

      [Test]
      public void GIVEN_refusals_built_up_WHEN_an_audience_is_granted_THEN_the_count_resets()
      {
         int reset = CompanionRetentionPolicy.OnAudienceGranted();
         reset.Should().Be(0);
         CompanionRetentionPolicy.Decide(true, true, reset).Should().Be(RetentionStep.RequestAudience);
      }

      // ── Out-of-patience predicate ──────────────────────────────────────────

      [Test]
      public void GIVEN_the_cap_WHEN_asked_THEN_they_are_out_of_patience()
      {
         CompanionRetentionPolicy.IsOutOfPatience(CompanionRetentionPolicy.MaxAudienceRefusals - 1).Should().BeFalse();
         CompanionRetentionPolicy.IsOutOfPatience(CompanionRetentionPolicy.MaxAudienceRefusals).Should().BeTrue();
      }
   }
}
