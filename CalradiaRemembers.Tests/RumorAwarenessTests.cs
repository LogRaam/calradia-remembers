// Code written by Gabriel Mailhot, 20/06/2026.
// Protects the awareness model — who plausibly knows what, and how surely (distortion with distance) —
// the logic behind rumour propagation and the "WHAT YOU'VE HEARD" hedging.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RumorAwarenessTests
   {
      // ── Reach × recency ────────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_fresh_nearby_in_faction_event_WHEN_scored_THEN_it_is_well_known()
      {
         //Arrange
         const int magnitude = 30;

         //Act
         var score = RumorAwareness.Score(magnitude, factionMatches: true, geoBonus: 25, ageDays: 0);

         //Assert
         score.Should().Be(80);                 // 30 + 25 (faction) + 25 (geo), recency 1.0
         RumorAwareness.Knows(score).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_faint_distant_out_of_faction_rumour_WHEN_scored_THEN_it_is_not_known()
      {
         //Arrange
         const int faintMagnitude = 18; // a whispered liaison

         //Act
         var score = RumorAwareness.Score(faintMagnitude, factionMatches: false, geoBonus: 0, ageDays: 0);

         //Assert
         RumorAwareness.Knows(score).Should().BeFalse("a faint rumour needs proximity or faction to travel");
      }

      [Test]
      public void GIVEN_a_mid_event_WHEN_it_touches_the_observers_faction_THEN_it_crosses_into_known()
      {
         //Arrange
         const int magnitude = 30;

         //Act
         var withoutFaction = RumorAwareness.Score(magnitude, factionMatches: false, geoBonus: 0, ageDays: 0);
         var withFaction    = RumorAwareness.Score(magnitude, factionMatches: true,  geoBonus: 0, ageDays: 0);

         //Assert
         RumorAwareness.Knows(withoutFaction).Should().BeFalse();
         RumorAwareness.Knows(withFaction).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_major_event_far_away_WHEN_scored_fresh_THEN_its_magnitude_carries_it()
      {
         //Arrange
         const int townFalls = 70;

         //Act
         var score = RumorAwareness.Score(townFalls, factionMatches: false, geoBonus: 0, ageDays: 0);

         //Assert
         RumorAwareness.Knows(score).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_event_older_than_the_recency_window_WHEN_scored_THEN_the_score_is_zero()
      {
         //Arrange
         const int magnitude = 80;

         //Act
         var score = RumorAwareness.Score(magnitude, factionMatches: true, geoBonus: 25, ageDays: RumorAwareness.RecencyWindowDays);

         //Assert
         score.Should().Be(0, "news fades to nothing once it is older than the recency window");
      }

      [Test]
      public void GIVEN_an_event_halfway_through_the_recency_window_WHEN_scored_THEN_reach_is_halved()
      {
         //Arrange
         const int reach = 80; // magnitude only, no faction/geo
         int halfway = RumorAwareness.RecencyWindowDays / 2;

         //Act
         var score = RumorAwareness.Score(reach, factionMatches: false, geoBonus: 0, ageDays: halfway);

         //Assert
         score.Should().Be(40);
      }

      // ── Confidence (distortion with distance) ──────────────────────────────

      [Test]
      public void GIVEN_a_high_score_WHEN_rated_THEN_it_is_well_known()
      {
         //Arrange/Act/Assert
         RumorAwareness.Rate(90).Should().Be(RumorConfidence.WellKnown);
      }

      [Test]
      public void GIVEN_a_middling_score_WHEN_rated_THEN_it_is_secondhand()
      {
         //Arrange/Act/Assert
         RumorAwareness.Rate(89).Should().Be(RumorConfidence.Secondhand);
         RumorAwareness.Rate(62).Should().Be(RumorConfidence.Secondhand);
      }

      [Test]
      public void GIVEN_a_score_just_over_the_threshold_WHEN_rated_THEN_it_is_a_distant_rumour()
      {
         //Arrange/Act/Assert
         RumorAwareness.Rate(61).Should().Be(RumorConfidence.Distant);
         RumorAwareness.Rate(RumorAwareness.Threshold).Should().Be(RumorConfidence.Distant);
      }

      // ── Knowing threshold ──────────────────────────────────────────────────

      [Test]
      public void GIVEN_a_score_at_the_threshold_WHEN_asked_THEN_the_observer_knows_it()
      {
         //Arrange/Act/Assert
         RumorAwareness.Knows(RumorAwareness.Threshold).Should().BeTrue();
         RumorAwareness.Knows(RumorAwareness.Threshold - 1).Should().BeFalse();
      }
   }
}
