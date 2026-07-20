// Code written by Gabriel Mailhot, 20/07/2026.
// Documents when a live courier's stillness becomes a jam (courier robustness batch, ROADMAP 2026-07-20). The
// stake: a jammed courier used to burn the whole delivery grace window and then LOSE the letter; these two
// predicates are what hands the letter back to date-based delivery early enough for it to still arrive.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CourierStuckPolicyTests
    {
        // Engine jitter around a jammed spot must not read as progress: a courier oscillating within the epsilon
        // would otherwise reset its clock every day and never be called stuck, recreating the very soft-lock the
        // watchdog exists to kill. The boundary is strict: drift AT the epsilon is still "did not move".
        [Test]
        public void GIVEN_drift_within_or_at_the_epsilon_WHEN_asked_THEN_it_does_not_count_as_movement()
        {
            CourierStuckPolicy.HasMoved(0f).Should().BeFalse();
            CourierStuckPolicy.HasMoved(CourierStuckPolicy.StuckEpsilon).Should().BeFalse();
        }

        // Real progress, however small beyond the epsilon, resets the jam clock: a slow courier crossing rough
        // terrain is late, not stuck, and reclaiming it would kill a ride that was actually working.
        [Test]
        public void GIVEN_progress_beyond_the_epsilon_WHEN_asked_THEN_it_counts_as_movement()
        {
            CourierStuckPolicy.HasMoved(CourierStuckPolicy.StuckEpsilon + 0.01f).Should().BeTrue();
        }

        // One bad pathing day (a detour, a blocked ford) must not cost the visible ride: the threshold is two
        // days, so a courier still on day one of stillness keeps its errand.
        [Test]
        public void GIVEN_one_day_of_stillness_WHEN_asked_THEN_the_courier_is_not_yet_stuck()
        {
            CourierStuckPolicy.IsStuck(today: 10, lastMovementDay: 9).Should().BeFalse();
        }

        // At the threshold the courier is stuck: its letter reverts to date-based delivery instead of waiting
        // out the grace window and being lost. This pins the boundary exactly at StuckDaysBeforeFallback.
        [Test]
        public void GIVEN_stillness_reaching_the_threshold_WHEN_asked_THEN_the_courier_is_stuck()
        {
            CourierStuckPolicy.IsStuck(today: 10, lastMovementDay: 10 - CourierStuckPolicy.StuckDaysBeforeFallback)
                .Should().BeTrue();
        }
    }
}
