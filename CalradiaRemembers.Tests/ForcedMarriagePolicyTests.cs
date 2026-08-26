// Code written by Gabriel Mailhot, 23/07/2026.
// Ratified 2026-07-23 (player report): a family that has given its blessing can bind its daughter for
// political ends, so a marriage below the regard bar now PROCEEDS instead of being silently refused, and what
// the unwon heart buys is a ForcedMarriage grudge scaled to the shortfall. This policy is the ONE place that
// scaling lives; BannerlordGameStateBridge.ExecuteMarry reads it right after sealing the union, and the grudge
// it plants is what flips the spouse's consent rules to the unwilling branch (the prompt's "no thresholds
// apply between spouses" exemption must NOT apply to a bride who never consented). Get the numbers wrong and
// either a near-miss bride resents like a kidnapped one, or the mechanic becomes a consent bypass with no
// cost at all.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ForcedMarriagePolicyTests
   {
      // The boundary that defines "forced": exactly the marriage bar every other gate already uses. At the bar
      // the union is a love match and plants nothing; one point below, it is political. If this drifted from
      // MarriageEligibilityThreshold, the wedding and the resentment would disagree about what just happened.
      [Test]
      public void GIVEN_regard_at_the_marriage_bar_WHEN_judging_the_union_THEN_it_is_not_forced()
      {
         ForcedMarriagePolicy.IsForcedUnion(MarriageEligibility.MarriageEligibilityThreshold).Should().BeFalse();
         ForcedMarriagePolicy.IsForcedUnion(MarriageEligibility.MarriageEligibilityThreshold - 1).Should().BeTrue();
      }

      // A willing bride must plant NOTHING, so the bridge can apply this unconditionally after every wedding
      // without special-casing the happy path. A nonzero value here would give every love match a grudge.
      [Test]
      public void GIVEN_a_love_match_WHEN_computing_the_resentment_THEN_it_is_zero()
      {
         ForcedMarriagePolicy.GrudgeMagnitude(MarriageEligibility.MarriageEligibilityThreshold).Should().Be(0);
         ForcedMarriagePolicy.GrudgeMagnitude(90).Should().Be(0);
      }

      // The scaling that makes the wound honest: a bride at 45 was nearly won, one at -20 was dragged to the
      // altar, and the two must not feel the same. Linear in the shortfall, so every point of unearned heart
      // costs exactly one point of grievance.
      [Test]
      public void GIVEN_a_colder_bride_WHEN_computing_the_resentment_THEN_it_grows_with_the_shortfall()
      {
         int nearMiss = ForcedMarriagePolicy.GrudgeMagnitude(45);
         int stranger = ForcedMarriagePolicy.GrudgeMagnitude(10);
         int enemy = ForcedMarriagePolicy.GrudgeMagnitude(-20);

         nearMiss.Should().BeLessThan(stranger);
         stranger.Should().BeLessThan(enemy);
         (stranger - nearMiss).Should().Be(35); // one point of grievance per point of shortfall
      }

      // A near-miss lands around the grudge-formation floor on purpose: faint enough that a merciful nature's
      // trait-raised floor may refuse it entirely (GrudgePolicy keeps the final say), never a heavy wound.
      [Test]
      public void GIVEN_a_bride_one_point_short_WHEN_computing_the_resentment_THEN_it_is_only_a_faint_grievance()
      {
         ForcedMarriagePolicy.GrudgeMagnitude(MarriageEligibility.MarriageEligibilityThreshold - 1)
            .Should().Be(ForcedMarriagePolicy.BaseResentment + 1);
      }

      // The design wants this arc WINNABLE: the cap sits below vendetta territory, so even a bride wed at the
      // bottom of the scale can eventually be won over. Uncapped, a deeply hostile match would start with a
      // grievance no amends could outlive, turning the redemption arc into a life sentence.
      [Test]
      public void GIVEN_a_bride_who_hates_the_player_WHEN_computing_the_resentment_THEN_it_stops_at_the_winnable_cap()
      {
         ForcedMarriagePolicy.GrudgeMagnitude(-100).Should().Be(ForcedMarriagePolicy.MaxResentment);
      }

      // The MCM "Marriage Regard Needed" setting lowers the bar the engine injects, so the seal gate and the
      // love-match gate must judge "forced" against that SAME lowered bar (or they drift: eligible per the
      // encounter, refused at the altar). A regard that clears a lowered bar is NOT a forced union.
      [Test]
      public void GIVEN_a_lowered_marriage_bar_WHEN_regard_clears_it_THEN_the_union_is_not_forced()
      {
         ForcedMarriagePolicy.IsForcedUnion(35, 35).Should().BeFalse(); // exactly at the lowered bar
         ForcedMarriagePolicy.IsForcedUnion(34, 35).Should().BeTrue();  // one short of it
      }

      // The resentment of a wedding forced at a LOWERED bar is measured against that same bar, so a bride wed
      // one point short of a 30 bar forms the same faint grievance she would one point short of 50, not a wound
      // sized to the shipped default she never had to clear.
      [Test]
      public void GIVEN_a_lowered_bar_WHEN_a_bride_is_wed_one_point_short_THEN_the_resentment_tracks_the_lowered_bar()
      {
         ForcedMarriagePolicy.GrudgeMagnitude(29, 30).Should().Be(ForcedMarriagePolicy.BaseResentment + 1);
      }
   }
}
