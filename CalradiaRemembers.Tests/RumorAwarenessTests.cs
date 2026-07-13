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

      // Pins the actual arithmetic (magnitude + faction bonus + geo bonus, times full recency at day 0):
      // this is the concrete score every NPC's "would I plausibly know this" gate runs on each prompt
      // build, so a formula regression here silently changes who is in the know realm-wide.
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

      // Without this floor, minor gossip would surface everywhere, breaking plausibility: an NPC on the
      // far side of Calradia, in an unrelated faction, has no way to have heard a small, faint event.
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

      // Pins FactionBonus as the actual swing factor that can flip Knows() for a mid-strength event: the
      // concrete case behind the "faction-weighted hearsay" half of the design.
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

      // A town falling must be knowable even out-of-faction and far away, on magnitude alone (unlike the
      // faint-rumour case above): the size-of-event floor that lets truly major news travel unaided.
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

      // Stale news must fade to NOTHING, not merely to a low score, once past the window; otherwise a
      // high-magnitude event could keep scoring above Threshold indefinitely and never stop being "known".
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

      // Pins recency as a LINEAR multiplier, not a cliff: catches a regression to step-function decay
      // that would make news vanish suddenly at some day instead of fading gradually toward it.
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

      // Confidence feeds the "WHAT YOU'VE HEARD" hedging: WellKnown lets an NPC state an event as fact,
      // so a wrong band here would have an NPC assert gossip they only barely heard as if it were certain.
      [Test]
      public void GIVEN_a_high_score_WHEN_rated_THEN_it_is_well_known()
      {
         //Arrange/Act/Assert
         RumorAwareness.Rate(90).Should().Be(RumorConfidence.WellKnown);
      }

      // Pins BOTH edges of the Secondhand band (89 and 62) so a future retune of WellKnownAt/SecondhandAt
      // cannot silently swallow this middle tier from either side.
      [Test]
      public void GIVEN_a_middling_score_WHEN_rated_THEN_it_is_secondhand()
      {
         //Arrange/Act/Assert
         RumorAwareness.Rate(89).Should().Be(RumorConfidence.Secondhand);
         RumorAwareness.Rate(62).Should().Be(RumorConfidence.Secondhand);
      }

      // Threshold (50) and SecondhandAt (62) are deliberately offset: an event can be barely "known"
      // (score >= Threshold) yet still rate as the least-confident tier, so the weakest known rumours
      // are voiced as uncertain hearsay rather than stated as solid fact.
      [Test]
      public void GIVEN_a_score_just_over_the_threshold_WHEN_rated_THEN_it_is_a_distant_rumour()
      {
         //Arrange/Act/Assert
         RumorAwareness.Rate(61).Should().Be(RumorConfidence.Distant);
         RumorAwareness.Rate(RumorAwareness.Threshold).Should().Be(RumorConfidence.Distant);
      }

      // ── Knowing threshold ──────────────────────────────────────────────────

      // Pins the exact Knows() cutoff (inclusive at Threshold, exclusive one below): this one boundary is
      // literally the gate between "this NPC has never heard of it" and "this NPC can speak of it" for
      // every event and observer the awareness model scores.
      [Test]
      public void GIVEN_a_score_at_the_threshold_WHEN_asked_THEN_the_observer_knows_it()
      {
         //Arrange/Act/Assert
         RumorAwareness.Knows(RumorAwareness.Threshold).Should().BeTrue();
         RumorAwareness.Knows(RumorAwareness.Threshold - 1).Should().BeFalse();
      }
   }
}
