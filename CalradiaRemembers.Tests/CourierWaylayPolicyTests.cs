// Code written by Gabriel Mailhot, 10/07/2026.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CourierWaylayPolicyTests
    {
        [TestCase(0, true)]
        [TestCase(32, true)]
        [TestCase(33, false)]
        [TestCase(99, false)]
        public void GIVEN_a_roll_WHEN_deciding_hostility_THEN_only_the_bottom_third_turns_hostile(int roll, bool hostile)
        {
            CourierWaylayPolicy.ShouldTurnHostile(roll).Should().Be(hostile);
        }

        [Test]
        public void GIVEN_a_negative_or_out_of_range_roll_WHEN_deciding_THEN_it_is_not_hostile()
        {
            CourierWaylayPolicy.ShouldTurnHostile(-1).Should().BeFalse();
            CourierWaylayPolicy.ShouldTurnHostile(100).Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_full_roll_range_WHEN_counting_hostiles_THEN_it_is_about_one_in_three()
        {
            var hostile = 0;
            for (var roll = 0; roll < 100; roll++)
                if (CourierWaylayPolicy.ShouldTurnHostile(roll))
                    hostile++;

            hostile.Should().Be(CourierWaylayPolicy.BanditHostileChancePercent);
        }

        [TestCase(0, true)]
        [TestCase(29, true)]
        [TestCase(30, false)]
        [TestCase(99, false)]
        public void GIVEN_a_chase_WHEN_deciding_the_catch_THEN_the_fast_courier_usually_escapes(int roll, bool caught)
        {
            CourierWaylayPolicy.ChaseCatchesCourier(roll).Should().Be(caught);
        }

        [Test]
        public void GIVEN_both_filters_WHEN_combined_THEN_the_effective_per_band_waylay_is_well_under_the_chase_rate()
        {
            // The two independent filters (gives chase AND catches) make interception rare and speed-rewarding.
            double effective = CourierWaylayPolicy.BanditHostileChancePercent / 100.0
                               * (CourierWaylayPolicy.ChaseCatchesCourierPercent / 100.0);
            effective.Should().BeLessThan(0.15);
        }
    }
}
