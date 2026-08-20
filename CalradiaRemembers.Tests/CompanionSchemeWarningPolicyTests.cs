// Code written by Gabriel Mailhot, 20/08/2026.
// Protects the SCHEME WARNING found-topic: a loyal companion warning the player that someone is secretly
// plotting against them (design brief). Get IsContentEnough wrong and a disaffected companion could be made to
// voice a warning they would not, in truth, go out of their way to deliver; get Strength too low and a live
// threat to the player could lose the audience to a mere thank-you or rumour, exactly what the "high-value,
// urgent" design intent rules out.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionSchemeWarningPolicyTests
   {
      // A companion comfortably content is exactly the case the whole topic exists for: loyal enough to risk
      // carrying an urgent warning to the player.
      [Test]
      public void GIVEN_a_content_companion_WHEN_checked_THEN_it_holds()
         => CompanionSchemeWarningPolicy.Holds(CompanionHappiness.Default).Should().BeTrue();

      // The design brief's own happiness gate: a disaffected companion would not go out of their way to protect
      // the player, so however real the plot, an unhappy companion must never be handed this topic.
      [Test]
      public void GIVEN_a_companion_who_is_not_content_WHEN_checked_THEN_it_does_not_hold()
         => CompanionSchemeWarningPolicy.Holds(CompanionHappiness.LeaveThreshold).Should().BeFalse();

      // IsContentEnough reuses CompanionHappiness.Band's own Content threshold (the same rule CompanionGratitudePolicy
      // relies on), so this can never disagree with the encyclopedia/prompt wording the player already sees for
      // this companion's mood.
      [Test]
      public void GIVEN_a_score_exactly_at_the_content_band_WHEN_asked_THEN_is_content_enough_agrees_with_the_band()
      {
         CompanionSchemeWarningPolicy.IsContentEnough(CompanionHappiness.Default).Should().BeTrue();
         CompanionSchemeWarningPolicy.IsContentEnough(CompanionHappiness.LeaveThreshold).Should().BeFalse();
      }

      // ── Strength ordering ────────────────────────────────────────────────────

      // Design brief: a live plot against the player is high-value and urgent, so it must outrank a mere
      // thank-you when both found-topics compete for the same audience. CompanionGratitudePolicyTests' own
      // "content, fresh, significant" example (MinEffectiveMagnitude + 10) is that policy's typical strength.
      [Test]
      public void GIVEN_a_typical_gratitude_strength_WHEN_compared_THEN_scheme_warning_outranks_it()
      {
         int typicalGratitude = CompanionGratitudePolicy.Strength(CompanionGratitudePolicy.MinEffectiveMagnitude + 10);

         CompanionSchemeWarningPolicy.Strength.Should().BeGreaterThan(typicalGratitude);
      }

      // Strength must stay inside every other found-topic's own 0..100 scale (CompanionCallbackPolicy,
      // CompanionRumourPolicy, CompanionGratitudePolicy), so it can never be read as an out-of-range outlier.
      [Test]
      public void GIVEN_the_pinned_strength_WHEN_checked_THEN_it_stays_within_0_100()
      {
         CompanionSchemeWarningPolicy.Strength.Should().BeInRange(0, 100);
      }
   }
}
