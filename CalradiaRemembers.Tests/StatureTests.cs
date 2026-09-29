// Code written by Gabriel Mailhot, 28/09/2026.
// Rank as a type (Gabriel, 28/09/2026). The engine-facts map found clan tier read by 28 rules under five names, each with
// its own marks; Stature gives the marks one home. It is a MOVE, not a retuning: every rung is a mark a rule already
// used, and the duel's station comes out of ChatViewModel unchanged. If a rung drifts, the rules that point at it move
// together, which is the point; if the station drifts, a duel is refused (or fought) across a gap it was not before.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class StatureTests
   {
      // The duel's station, exactly as ChatViewModel.DuelStation computed it before the move: ruler 3, tier 4+ 2,
      // tier 1+ 1, otherwise 0. A duel between stations two apart is dismissed rather than fought.
      [TestCase(0, false, 0)]
      [TestCase(1, false, 1)]
      [TestCase(3, false, 1)]
      [TestCase(4, false, 2)]
      [TestCase(6, false, 2)]
      [TestCase(0, true, 3)]
      [TestCase(6, true, 3)]
      public void GIVEN_a_clan_tier_and_rulership_WHEN_the_duel_station_is_read_THEN_it_is_as_it_always_was(int tier, bool ruler, int station)
      {
         new Stature(tier, ruler).Station.Should().Be(station);
      }

      // The gap the duel refuses on: how far the challenger stands beneath the one challenged, never negative.
      [Test]
      public void GIVEN_two_statures_WHEN_the_station_gap_is_read_THEN_it_counts_only_how_far_the_lower_stands_beneath()
      {
         Stature.StationsBelow(new Stature(0, true), new Stature(0, false)).Should().Be(3);
         Stature.StationsBelow(new Stature(0, false), new Stature(6, true)).Should().Be(0);
      }

      // The three marks the rules already used, now named once: the peace mediator's 3 and the great house's 4 are the
      // same numbers the rules held before the move.
      [Test]
      public void GIVEN_the_named_rungs_WHEN_read_THEN_they_are_the_marks_the_rules_already_used()
      {
         Stature.EstablishedTier.Should().Be(1);
         Stature.LandedNobleTier.Should().Be(3);
         Stature.GreatHouseTier.Should().Be(4);
         PeaceMediationPolicy.StatureTierFloor.Should().Be(Stature.LandedNobleTier);
         CalradiaRemembers.Logic.Battles.CombatProwessPolicy.FamousClanTier.Should().Be(Stature.GreatHouseTier);
         CorrespondenceRankPolicy.TierARulerAddressesInPerson.Should().Be(Stature.GreatHouseTier);
      }

      // A clan tier the game reports below zero reads as none, so a gap is never inflated by a bad value.
      [Test]
      public void GIVEN_a_negative_tier_WHEN_a_stature_is_made_THEN_it_reads_as_no_standing()
      {
         new Stature(-2, false).ClanTier.Should().Be(0);
         new Stature(5, false).TiersAbove(new Stature(2, false)).Should().Be(3);
      }
   }
}
