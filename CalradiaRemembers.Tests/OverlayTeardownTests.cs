// Code written by Gabriel Mailhot, 21/06/2026.
// Protects the chat-overlay close lifecycle (the "garbage-collected" teardown that fixes the
// letter-reply crash): the map-vs-mission branch, finalize-then-defer, destroy-exactly-once, and
// clearing a stale flag so a fresh overlay is never destroyed. The irreducible RemoveLayer timing is
// validated in game; this proves the state machine that drives it.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class OverlayTeardownTests
    {
        [Test]
        public void GIVEN_open_on_the_map_WHEN_requesting_close_THEN_it_finalizes_unpauses_and_waits_for_a_tick()
        {
            //Arrange
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();

            //Act
            teardown.RequestClose(fx);

            //Assert — prepared and pending, but NOT yet destroyed (that waits for the tick).
            fx.Actions.Should().Equal("Finalize", "Unpause");
            teardown.IsPending.Should().BeTrue();
        }

        [Test]
        public void GIVEN_in_a_mission_WHEN_requesting_close_THEN_it_closes_synchronously_without_deferring()
        {
            //Arrange — no campaign tick to defer to in a mission/scene.
            var fx = new SpyEffects { IsOpen = true, InMission = true };
            var teardown = new OverlayTeardown();

            //Act
            teardown.RequestClose(fx);

            //Assert
            fx.Actions.Should().Equal("SyncClose");
            teardown.IsPending.Should().BeFalse();
        }

        [Test]
        public void GIVEN_the_overlay_is_not_open_WHEN_requesting_close_THEN_nothing_happens()
        {
            var fx = new SpyEffects { IsOpen = false, InMission = false };
            var teardown = new OverlayTeardown();

            teardown.RequestClose(fx);

            fx.Actions.Should().BeEmpty();
            teardown.IsPending.Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_close_already_pending_WHEN_requesting_again_THEN_it_does_not_prepare_twice()
        {
            //Arrange
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();
            teardown.RequestClose(fx);
            fx.Actions.Clear();

            //Act — a second request while pending.
            teardown.RequestClose(fx);

            //Assert
            fx.Actions.Should().BeEmpty();
        }

        [Test]
        public void GIVEN_a_pending_close_WHEN_ticking_THEN_it_destroys_once_and_clears_pending()
        {
            //Arrange
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();
            teardown.RequestClose(fx);
            fx.Actions.Clear();

            //Act
            teardown.Tick(fx);

            //Assert
            fx.Actions.Should().Equal("Destroy");
            teardown.IsPending.Should().BeFalse();
        }

        [Test]
        public void GIVEN_a_pending_close_but_the_overlay_already_gone_WHEN_ticking_THEN_it_clears_without_destroying()
        {
            //Arrange — pending, but something else closed the overlay first.
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();
            teardown.RequestClose(fx);
            fx.Actions.Clear();
            fx.IsOpen = false;

            //Act
            teardown.Tick(fx);

            //Assert
            fx.Actions.Should().BeEmpty();
            teardown.IsPending.Should().BeFalse();
        }

        [Test]
        public void GIVEN_no_pending_close_WHEN_ticking_THEN_nothing_happens()
        {
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            new OverlayTeardown().Tick(fx);
            fx.Actions.Should().BeEmpty();
        }

        [Test]
        public void GIVEN_a_pending_close_WHEN_ticking_twice_THEN_it_destroys_only_once()
        {
            //Arrange
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();
            teardown.RequestClose(fx);
            fx.Actions.Clear();

            //Act
            teardown.Tick(fx);
            teardown.Tick(fx);

            //Assert
            fx.Actions.Should().Equal("Destroy");
        }

        [Test]
        public void GIVEN_a_stale_pending_flag_WHEN_reset_THEN_a_later_tick_does_not_destroy_a_fresh_overlay()
        {
            //Arrange — the overlay was freed by another path; Reset clears the flag.
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();
            teardown.RequestClose(fx);
            fx.Actions.Clear();

            //Act
            teardown.Reset();
            teardown.Tick(fx);

            //Assert
            teardown.IsPending.Should().BeFalse();
            fx.Actions.Should().BeEmpty();
        }

        [Test]
        public void GIVEN_a_full_map_cycle_WHEN_request_then_tick_THEN_prepare_precedes_a_single_destroy_and_it_can_repeat()
        {
            //Arrange
            var fx = new SpyEffects { IsOpen = true, InMission = false };
            var teardown = new OverlayTeardown();

            //Act — first close.
            teardown.RequestClose(fx);
            teardown.Tick(fx);

            //Assert
            fx.Actions.Should().Equal("Finalize", "Unpause", "Destroy");

            //Act — a second, independent close works the same.
            fx.Actions.Clear();
            teardown.RequestClose(fx);
            teardown.Tick(fx);
            fx.Actions.Should().Equal("Finalize", "Unpause", "Destroy");
        }

        #region private

        private sealed class SpyEffects : IOverlayTeardownEffects
        {
            public List<string> Actions { get; } = new List<string>();
            public bool IsOpen { get; set; }
            public bool InMission { get; set; }

            public void FinalizeSession() => Actions.Add("Finalize");
            public void UnpauseForTick() => Actions.Add("Unpause");
            public void DestroyNow() => Actions.Add("Destroy");
            public void SynchronousClose() => Actions.Add("SyncClose");
        }

        #endregion
    }
}
