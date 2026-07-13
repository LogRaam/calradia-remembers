// Code written by Gabriel Mailhot, 28/06/2026.
// Protects the companion-retention loop: an unhappy companion asks for an audience, a granted audience clears the
// grievance, refusals accumulate, and after the cap the companion leaves by farewell — never silently.
//
// WHY IT MATTERS: this is the design rule from CompanionRetentionPolicy's own header, that NO companion ever
// departs without warning. Get the toggle wrong and the MCM "off" switch (the very thing SuperGraham339 reached
// for when the old model guaranteed departure) still fires audience popups. Get the cap wrong and a companion
// either leaves after a single refusal, or can be ignored forever without ever actually walking out.

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

      // The MCM "Contentment" off-switch must be a hard gate: a player who disables the system (as the original
      // reporter did) must never still get audience popups from a companion the system is supposed to ignore.
      [Test]
      public void GIVEN_contentment_disabled_WHEN_decided_THEN_nothing_happens_even_if_unhappy()
      {
         CompanionRetentionPolicy.Decide(false, true, 0).Should().Be(RetentionStep.None);
         CompanionRetentionPolicy.Decide(false, true, CompanionRetentionPolicy.MaxAudienceRefusals).Should().Be(RetentionStep.None);
      }

      // A companion who is not actually unhappy must never be pestered with an audience request; the loop only
      // exists to warn the player before losing someone who genuinely wants to leave.
      [Test]
      public void GIVEN_a_content_companion_WHEN_decided_THEN_nothing_happens()
         => CompanionRetentionPolicy.Decide(true, false, 0).Should().Be(RetentionStep.None);

      // ── Audience requests ──────────────────────────────────────────────────

      // This is the "never silently" guarantee firing: an unhappy companion below the cap must ASK, giving the
      // player a real chance to intervene before the relationship is lost for good.
      [Test]
      public void GIVEN_an_unhappy_companion_below_the_cap_WHEN_decided_THEN_they_ask_for_an_audience()
      {
         CompanionRetentionPolicy.Decide(true, true, 0).Should().Be(RetentionStep.RequestAudience);
         CompanionRetentionPolicy.Decide(true, true, CompanionRetentionPolicy.MaxAudienceRefusals - 1)
            .Should().Be(RetentionStep.RequestAudience);
      }

      // ── Patience runs out ──────────────────────────────────────────────────

      // The actual departure trigger. Get MaxAudienceRefusals wrong and a companion either leaves after being
      // ignored once, or the player can refuse indefinitely and never actually lose them.
      [Test]
      public void GIVEN_the_refusal_cap_is_reached_WHEN_decided_THEN_they_send_a_farewell()
         => CompanionRetentionPolicy.Decide(true, true, CompanionRetentionPolicy.MaxAudienceRefusals)
            .Should().Be(RetentionStep.SendFarewell);

      // ── Refusal accumulation ───────────────────────────────────────────────

      // If refusals stopped accumulating, the SendFarewell branch of Decide would be unreachable and an ignored
      // companion could never actually leave: the counter is what makes the cap mean anything.
      [Test]
      public void GIVEN_a_refused_audience_WHEN_registered_THEN_the_count_rises_by_one()
      {
         CompanionRetentionPolicy.RegisterRefusal(0).Should().Be(1);
         CompanionRetentionPolicy.RegisterRefusal(2).Should().Be(3);
      }

      // End-to-end pin of the cap across Decide and RegisterRefusal together: MaxAudienceRefusals=3 must mean
      // exactly three refusals are tolerated, with the farewell firing on the fourth decide, not sooner or later.
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

      // Hearing a companion out must fully forgive prior refusals; otherwise a companion who was finally granted
      // an audience could still be doomed by refusals racked up before the reconciliation.
      [Test]
      public void GIVEN_refusals_built_up_WHEN_an_audience_is_granted_THEN_the_count_resets()
      {
         int reset = CompanionRetentionPolicy.OnAudienceGranted();
         reset.Should().Be(0);
         CompanionRetentionPolicy.Decide(true, true, reset).Should().Be(RetentionStep.RequestAudience);
      }

      // ── Out-of-patience predicate ──────────────────────────────────────────

      // Not exercised by Decide itself (per the production doc, it answers "will they leave on the NEXT decide");
      // pins that its own bar matches the cap exactly, so a caller reading it can never disagree with Decide.
      [Test]
      public void GIVEN_the_cap_WHEN_asked_THEN_they_are_out_of_patience()
      {
         CompanionRetentionPolicy.IsOutOfPatience(CompanionRetentionPolicy.MaxAudienceRefusals - 1).Should().BeFalse();
         CompanionRetentionPolicy.IsOutOfPatience(CompanionRetentionPolicy.MaxAudienceRefusals).Should().BeTrue();
      }
   }
}
