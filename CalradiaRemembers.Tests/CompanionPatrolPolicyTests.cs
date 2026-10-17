// Code written by Gabriel Mailhot, 17/10/2026.
// The companion patrol's rules (jaserms1, 16/10/2026; Gabriel's rulings 16-17/10; his engine spike of 17/10 at
// Ebereth). Wrong, the patrol wanders off across the map as it did in the spike, breaks off a fight or a flight to
// ride back, starves at its post, or strips the player's party bare before the player has chosen anyone.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CompanionPatrolPolicyTests
    {
        // STAKE: the spike's drift: the behaviour still read "patrol" while the patrol had been re-anchored elsewhere,
        // 117 away in three days. A patrol anchored on another place is sent back even close by.
        [Test]
        public void GIVEN_a_patrol_re_anchored_elsewhere_WHEN_the_hour_ticks_THEN_it_is_sent_back_to_its_settlement()
        {
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {Patrolling = true, AnchoredOnOrderedSettlement = false, DistanceToSettlement = 3f})
                                 .Should().BeTrue();
        }

        // STAKE: a patrol that wandered too far is brought back, but one moving about its settlement is left alone
        // (re-issuing every hour would make it twitch on the spot).
        [Test]
        public void GIVEN_a_patrol_anchored_on_its_settlement_WHEN_the_hour_ticks_THEN_only_a_far_one_is_sent_back()
        {
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {Patrolling = true, AnchoredOnOrderedSettlement = true, DistanceToSettlement = 8f})
                                 .Should().BeFalse();
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {Patrolling = true, AnchoredOnOrderedSettlement = true, DistanceToSettlement = CompanionPatrolPolicy.LeashDistance + 1f})
                                 .Should().BeTrue();
        }

        // STAKE: the patrol exists to fight; a leash that tugs it back mid-chase or mid-flight gets its men killed.
        [Test]
        public void GIVEN_a_patrol_in_battle_chasing_or_fleeing_WHEN_the_hour_ticks_THEN_it_is_left_alone()
        {
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {InBattle = true, DistanceToSettlement = 50f}).Should().BeFalse();
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {EngagingOrFleeing = true, DistanceToSettlement = 50f}).Should().BeFalse();
        }

        // STAKE: in the spike the AI took the band off to a town for half a day; that is called back, unless the band
        // is out of food, when the trip keeps the men from starving at their post.
        [Test]
        public void GIVEN_a_band_that_left_its_patrol_WHEN_the_hour_ticks_THEN_it_is_called_back_unless_it_went_for_food()
        {
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {Patrolling = false}).Should().BeTrue();
            CompanionPatrolPolicy.ShouldReissue(new PatrolLeashFacts {Patrolling = false, FoodLow = true}).Should().BeFalse();
        }

        // STAKE: the troop screen opens with the men the order named (Gabriel: a guard of 250 for a town at war),
        // else a modest band, and never strips more than half the party unasked; the player adjusts on the screen.
        [Test]
        public void GIVEN_an_order_WHEN_the_troop_screen_is_prefilled_THEN_it_holds_the_named_men_else_a_modest_band()
        {
            CompanionPatrolPolicy.PrefillMen(250, 300).Should().Be(250);
            CompanionPatrolPolicy.PrefillMen(250, 120).Should().Be(120);
            CompanionPatrolPolicy.PrefillMen(null, 100).Should().Be(CompanionPatrolPolicy.DefaultBand);
            CompanionPatrolPolicy.PrefillMen(null, 30).Should().Be(15);
            CompanionPatrolPolicy.PrefillMen(null, 0).Should().Be(0);
        }

        // STAKE: Gabriel: each month the companion writes with news and asks whether to go on; not before, not never.
        [Test]
        public void GIVEN_days_since_the_last_letter_WHEN_asked_THEN_a_letter_is_due_after_a_month()
        {
            CompanionPatrolPolicy.ReportDue(100, 129).Should().BeFalse();
            CompanionPatrolPolicy.ReportDue(100, 130).Should().BeTrue();
        }

        // STAKE: the band rides out fed from the player's own stock (moved, never conjured), enough for a few days.
        [Test]
        public void GIVEN_a_band_WHEN_it_rides_out_THEN_it_takes_a_few_days_of_food_at_the_engine_rate()
        {
            CompanionPatrolPolicy.FoodUnitsForDeparture(40, 0.05f).Should().Be(10);
            CompanionPatrolPolicy.FoodUnitsForDeparture(0, 0.05f).Should().Be(0);
        }
    }
}
