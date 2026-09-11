// Code written by Gabriel Mailhot, 08/09/2026.
// What this pins: when ChatManager may refuse to open a conversation because one is already open, and when that
// same recorded window must instead be treated as a leftover and cleared.
//
// Why it exists. A player on 2.5.3 reported (Nexus, 2026-09-08): "a companion approaches me when i enter a
// village, a CR chat window doesnt appear... clicking 'a word with you' doesnt open the CR chat window and just
// ends the conversation. and then after that, no CR chat windows with anyone work unless i fully quit and
// restart the game." He saw it twice.
//
// The last sentence is the bug. Close() is written with real care, so the trap was never a close that failed: it
// was an open whose close never came, leaving a layer recorded against a screen that had since gone. "Already
// open" then stayed true for the rest of the session, every entry no-opped in silence, and the vanilla dialogue,
// with nowhere to route, simply ended. One bad moment cost the player the whole feature until he restarted.
//
// The asymmetry below is the entire point, so it is worth stating plainly: refusing is right ONLY when a window
// is genuinely there, because a second window over a live one would strand both. In every other case refusing is
// the more damaging choice, and clearing costs at most one conversation.
//
// EXTENDED 2026-09-09. A second player (tashmetu, v2.5.6) reported the same ending, "it stays broken for every
// subsequent conversation until the game is restarted", against a build that already carried the fix above. The
// first rule asked only whether the layer was still in its host screen's layer list, and the engine FINALIZES a
// screen without emptying that list: a chat stranded on a mission that had since ended read as perfectly live and
// refused every conversation exactly as before. Host liveness is now the third fact, and the cases below say what
// each of the three can and cannot do on its own.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ChatWindowLivenessPolicyTests
    {
        // The one case where refusing is correct. Opening a second window over a live one would leave two layers
        // fighting for focus and no clean way to close either.
        [Test]
        public void GIVEN_a_window_that_is_genuinely_open_WHEN_opening_again_THEN_the_request_is_refused()
        {
            ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = true, HostScreenAlive = true})
                .Should().BeTrue();
            ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = true, HostScreenAlive = true})
                .Should().BeFalse();
        }

        // The reported bug, pinned. A layer recorded against a screen that has gone must NOT refuse the next
        // conversation: that is precisely how one failure in a village killed every conversation for a session.
        [Test]
        public void GIVEN_a_layer_left_on_a_screen_that_is_gone_WHEN_opening_THEN_it_is_a_ghost_and_never_refuses()
        {
            ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = false, HostScreenAlive = true})
                .Should().BeTrue();
            ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = false, HostScreenAlive = true})
                .Should().BeFalse();
        }

        // tashmetu's ending, pinned. The layer is STILL in the list, because a finalized screen keeps its layers;
        // only the screen's own death tells the truth. Without this fact the window below is indistinguishable
        // from a live one, and refusing on its account is exactly what costs a player the rest of his session.
        [Test]
        public void GIVEN_a_layer_still_listed_on_a_screen_that_has_died_WHEN_opening_THEN_it_is_a_ghost_not_a_window()
        {
            ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = true, HostScreenAlive = false})
                .Should().BeTrue();
            ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = true, HostScreenAlive = false})
                .Should().BeFalse();
        }

        // Neither fact may be read as permission on its own: a window is live only when BOTH hold. Stated as its
        // own case because the failure this file exists for was, twice, one half of the question being asked.
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void GIVEN_either_half_of_liveness_is_missing_WHEN_opening_THEN_nothing_is_refused(bool onScreen, bool hostAlive)
        {
            ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = onScreen, HostScreenAlive = hostAlive}).Should().BeFalse();
            ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = true, LayerStillOnHostScreen = onScreen, HostScreenAlive = hostAlive}).Should().BeTrue();
        }

        // The ordinary path must stay ordinary: with nothing recorded there is nothing to refuse and nothing to
        // clean up, so the overwhelming majority of opens pay nothing for any of this.
        [Test]
        public void GIVEN_no_window_recorded_WHEN_opening_THEN_it_neither_refuses_nor_cleans()
        {
            ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = false, LayerStillOnHostScreen = false, HostScreenAlive = false}).Should().BeFalse();
            ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = false, LayerStillOnHostScreen = false, HostScreenAlive = false}).Should().BeFalse();
            ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = false, LayerStillOnHostScreen = true, HostScreenAlive = true}).Should().BeFalse();
            ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = false, LayerStillOnHostScreen = true, HostScreenAlive = true}).Should().BeFalse();
        }

        // The two answers must never both be true: one says "leave it alone", the other says "tear it down", and
        // a state that claimed both would send the caller down both paths in the same breath.
        [TestCase(true, true, true)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, true)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, false, false)]
        public void GIVEN_any_state_WHEN_asked_THEN_refusing_and_ghosting_are_never_both_true(bool recorded, bool onScreen, bool hostAlive)
        {
            (ChatWindowLivenessPolicy.ShouldRefuseOpen(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = recorded, LayerStillOnHostScreen = onScreen, HostScreenAlive = hostAlive})
             && ChatWindowLivenessPolicy.IsGhost(new ChatWindowLivenessPolicy.WindowRecord {HasRecordedLayer = recorded, LayerStillOnHostScreen = onScreen, HostScreenAlive = hostAlive})).Should().BeFalse();
        }
    }
}
