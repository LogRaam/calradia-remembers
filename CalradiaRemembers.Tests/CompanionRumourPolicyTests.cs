// Code written by Gabriel Mailhot, 22/07/2026.
// Protects the RUMOUR REPORT found-topic's strength scoring. WorldEventStore already decides WHETHER a
// companion could plausibly have heard an event (RumorAwareness) and how surely (RumorConfidence); this only
// turns that existing rating into the shared 0..100 scale the other found-topics compete on, so a rumour is
// neither an automatic winner nor an automatic loser against a callback or a grudge.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionRumourPolicyTests
   {
      // A well-known rumour is worth voicing more than a barely-caught whisper of the SAME underlying event:
      // the companion is more confident bringing it up, so it should rank higher against other found-topics.
      [Test]
      public void GIVEN_the_same_magnitude_WHEN_confidence_differs_THEN_well_known_scores_higher_than_distant()
      {
         int wellKnown = CompanionRumourPolicy.Strength(50, RumorConfidence.WellKnown);
         int distant = CompanionRumourPolicy.Strength(50, RumorConfidence.Distant);

         wellKnown.Should().BeGreaterThan(distant);
      }

      // ── Clamped to the shared 0..100 scale ───────────────────────────────────

      // Every found-topic strength is compared on the same 0..100 scale (CompanionAudienceTopicPolicy); a
      // magnitude near the ceiling nudged upward by confidence must never spill past 100.
      [Test]
      public void GIVEN_a_high_magnitude_WHEN_well_known_THEN_strength_never_exceeds_100()
         => CompanionRumourPolicy.Strength(100, RumorConfidence.WellKnown).Should().Be(100);

      // A faint, doubtful rumour of a low-magnitude event must never go negative on the shared scale.
      [Test]
      public void GIVEN_a_low_magnitude_WHEN_distant_THEN_strength_never_goes_below_zero()
         => CompanionRumourPolicy.Strength(0, RumorConfidence.Distant).Should().Be(0);
   }
}
