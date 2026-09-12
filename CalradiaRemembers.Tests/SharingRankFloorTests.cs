// Code written by Gabriel Mailhot, 12/09/2026.
// What these pin: that a partner whose own culture accepts the arrangement cannot be ground into hatred by it.
//
// Player report, 2026-09-12: "Even if an NPC from the Empire accepts polyamory, their affinity continues to
// drop. Since there is no specific information regarding jealousy, it is possible that a recent update -
// intended to align views on romance with the culture - has caused an internal conflict or inconsistency."
//
// He was half right, and it is worth being exact about which half, because the two halves have different
// fixes.
//
// NOT BROKEN: the v2.5.5 culture change works. A tolerant partner already takes 3/6/8 where an exclusive one
// takes 10/25/40, because by their custom the grievance is RANK AND ATTENTION rather than betrayal - the
// letter context has said so in as many words since the day it shipped.
//
// BROKEN: nothing bounded it. Small slights accumulate, and over a long campaign a partner who ACCEPTS the
// arrangement slides all the way to -100, one -3 at a time. Being set below another consort should cost
// warmth. It must not manufacture hatred for a practice their own people keep.
//
// The floor is ZERO rather than a negative: they may cool completely and still not become an enemy over it. A
// real betrayal is a different stake and keeps the old floor of -100 - that is the line this file guards.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SharingRankFloorTests
   {
      // The report itself: a warm partner in a sharing culture cools, and the cooling stops.
      [Test]
      public void GIVEN_a_partner_whose_people_accept_it_WHEN_slighted_again_and_again_THEN_they_cool_but_never_hate()
      {
         int regard = 40;

         for (var slight = 0; slight < 40; slight++)
            regard += JealousyReactionPolicy.SharingRankDelta(regard, -6);

         regard.Should().Be(JealousyReactionPolicy.SharingRankFloor);
         regard.Should().Be(0);
      }

      // The floor is a floor, not a clamp that eats the first slight: warmth is still lost, and lost at the
      // full rate, right up to the bottom.
      [Test]
      public void GIVEN_a_partner_still_warm_WHEN_slighted_THEN_the_slight_costs_exactly_what_it_should()
      {
         JealousyReactionPolicy.SharingRankDelta(40, -6).Should().Be(-6);
         JealousyReactionPolicy.SharingRankDelta(8, -6).Should().Be(-6);
      }

      // The last step lands ON the floor rather than through it. A partner at 3 loses 3, not 8.
      [Test]
      public void GIVEN_a_partner_nearly_cooled_WHEN_slighted_THEN_the_fall_stops_at_the_floor()
      {
         JealousyReactionPolicy.SharingRankDelta(3, -8).Should().Be(-3);
      }

      // And once they are cold, the grievance costs no more regard - but it is NOT suppressed. The memory and
      // the letter still happen, because being overlooked is still worth saying; it simply stops being paid
      // for in a currency they have run out of.
      [Test]
      public void GIVEN_a_partner_already_cold_WHEN_slighted_again_THEN_it_costs_nothing_further()
      {
         JealousyReactionPolicy.SharingRankDelta(0, -8).Should().Be(0);
         JealousyReactionPolicy.SharingRankDelta(-20, -8).Should().Be(0);
      }

      // A partner already driven low by something ELSE is not healed by this. The floor withholds a fall; it
      // never hands anybody regard back, which would let a slight read as a kindness.
      [Test]
      public void GIVEN_a_partner_below_the_floor_WHEN_slighted_THEN_nothing_is_given_back()
      {
         JealousyReactionPolicy.SharingRankDelta(-50, -8).Should().Be(0);
         JealousyReactionPolicy.SharingRankDelta(-50, -8).Should().BeLessThanOrEqualTo(0);
      }

      // Guarding the boundary of the rule itself: this applies to the SHARING grievance only. A betrayal is a
      // different stake, keeps the -100 floor, and never comes through here - so a positive or zero delta is
      // passed through untouched rather than being quietly reinterpreted.
      [Test]
      public void GIVEN_anything_that_is_not_a_loss_WHEN_measured_THEN_the_rule_leaves_it_alone()
      {
         JealousyReactionPolicy.SharingRankDelta(40, 0).Should().Be(0);
         JealousyReactionPolicy.SharingRankDelta(40, 5).Should().Be(5);
         JealousyReactionPolicy.SharingRankDelta(-90, 5).Should().Be(5);
      }
   }
}
