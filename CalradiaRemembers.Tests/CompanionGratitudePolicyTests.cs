// Code written by Gabriel Mailhot, 20/08/2026.
// Protects the GRATITUDE found-topic: a content companion thanking the player for a specific kindness, purely
// warm, never a request or a bargain (design brief). Get MinEffectiveMagnitude wrong and every trivial gift
// pulls the player aside for a whole audience; get RecencyWindowDays wrong and a companion thanks the player
// for something from many months ago, which reads as delayed rather than heartfelt; get IsContentEnough wrong
// and an unhappy companion could be made to voice a false thank-you, which the design brief explicitly calls
// "wrong".

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CompanionGratitudePolicyTests
   {
      // A companion comfortably content, thanking for a fresh and significant kindness, is exactly the case the
      // whole topic exists for.
      [Test]
      public void GIVEN_a_significant_recent_appreciation_and_a_content_companion_WHEN_checked_THEN_it_holds()
         => CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = CompanionGratitudePolicy.MinEffectiveMagnitude + 10, DaysAgo = 5, HappinessScore = CompanionHappiness.Default})
                                    .Should().BeTrue();

      // ── Magnitude floor ──────────────────────────────────────────────────────

      // Below the floor, the kindness is too slight to justify a whole private audience over, routine warmth,
      // not the "genuinely meaningful kindness" the design brief asks for.
      [Test]
      public void GIVEN_a_magnitude_below_the_floor_WHEN_checked_THEN_it_does_not_hold()
         => CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = CompanionGratitudePolicy.MinEffectiveMagnitude - 1, DaysAgo = 5, HappinessScore = CompanionHappiness.Default})
                                    .Should().BeFalse();

      // The floor itself is inclusive: a kindness exactly AT the threshold still qualifies.
      [Test]
      public void GIVEN_a_magnitude_exactly_at_the_floor_WHEN_checked_THEN_it_holds()
         => CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = CompanionGratitudePolicy.MinEffectiveMagnitude, DaysAgo = 5, HappinessScore = CompanionHappiness.Default})
                                    .Should().BeTrue();

      // ── Recency window ───────────────────────────────────────────────────────

      // A kindness thanked for many months late reads as delayed, not heartfelt (design brief). Beyond the
      // window, Gratitude must stay silent rather than dredge it up.
      [Test]
      public void GIVEN_an_appreciation_older_than_the_recency_window_WHEN_checked_THEN_it_does_not_hold()
         => CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = 100, DaysAgo = CompanionGratitudePolicy.RecencyWindowDays + 1, HappinessScore = CompanionHappiness.Default})
                                    .Should().BeFalse();

      // The window's own edge still counts as "recent enough": a boundary test so the constant's exact meaning
      // (inclusive ceiling) is pinned rather than left to guesswork.
      [Test]
      public void GIVEN_an_appreciation_exactly_at_the_recency_window_edge_WHEN_checked_THEN_it_still_holds()
         => CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = 100, DaysAgo = CompanionGratitudePolicy.RecencyWindowDays, HappinessScore = CompanionHappiness.Default})
                                    .Should().BeTrue();

      // ── Happiness gate ───────────────────────────────────────────────────────

      // The design brief, verbatim: "a thank-you from an unhappy companion is wrong". However strong and fresh
      // the kindness, a restless or disgruntled companion must never be handed this topic.
      [Test]
      public void GIVEN_a_companion_who_is_not_content_WHEN_checked_THEN_it_does_not_hold_even_with_a_strong_appreciation()
         => CompanionGratitudePolicy.Holds(new CompanionGratitudePolicy.GratitudeFacts {EffectiveMagnitude = 100, DaysAgo = 0, HappinessScore = CompanionHappiness.LeaveThreshold}).Should().BeFalse();

      // IsContentEnough reuses CompanionHappiness.Band's own Content threshold, so this can never disagree with
      // the encyclopedia/prompt wording the player already sees for this companion's mood.
      [Test]
      public void GIVEN_a_score_exactly_at_the_content_band_WHEN_asked_THEN_is_content_enough_agrees_with_the_band()
      {
         // CompanionHappiness.Default (60) sits inside the Content band; the leave floor sits inside Disgruntled.
         CompanionGratitudePolicy.IsContentEnough(CompanionHappiness.Default).Should().BeTrue();
         CompanionGratitudePolicy.IsContentEnough(CompanionHappiness.LeaveThreshold).Should().BeFalse();
      }

      // ── Strength ordering ────────────────────────────────────────────────────

      // "Strength scales with the appreciation's magnitude, so a bigger kindness outranks a smaller topic"
      // (design brief): a stronger kindness must never score lower than a weaker one when found-topics compete.
      [Test]
      public void GIVEN_two_appreciations_of_different_magnitude_WHEN_scored_THEN_the_bigger_kindness_scores_higher()
      {
         int smaller = CompanionGratitudePolicy.Strength(45);
         int bigger = CompanionGratitudePolicy.Strength(90);

         bigger.Should().BeGreaterThan(smaller);
      }

      // Strength is directly comparable against every other found-topic's own 0..100 scale (CompanionCallbackPolicy,
      // CompanionRumourPolicy), so it must never escape that range even on a malformed input.
      [Test]
      public void GIVEN_an_out_of_range_magnitude_WHEN_scored_THEN_strength_stays_clamped_to_0_100()
      {
         CompanionGratitudePolicy.Strength(-10).Should().Be(0);
         CompanionGratitudePolicy.Strength(150).Should().Be(100);
      }
   }
}
