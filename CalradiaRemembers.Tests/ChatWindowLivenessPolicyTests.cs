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
            ChatWindowLivenessPolicy.ShouldRefuseOpen(hasRecordedLayer: true, layerStillOnHostScreen: true)
                .Should().BeTrue();
            ChatWindowLivenessPolicy.IsGhost(hasRecordedLayer: true, layerStillOnHostScreen: true)
                .Should().BeFalse();
        }

        // The reported bug, pinned. A layer recorded against a screen that has gone must NOT refuse the next
        // conversation: that is precisely how one failure in a village killed every conversation for a session.
        [Test]
        public void GIVEN_a_layer_left_on_a_screen_that_is_gone_WHEN_opening_THEN_it_is_a_ghost_and_never_refuses()
        {
            ChatWindowLivenessPolicy.IsGhost(hasRecordedLayer: true, layerStillOnHostScreen: false)
                .Should().BeTrue();
            ChatWindowLivenessPolicy.ShouldRefuseOpen(hasRecordedLayer: true, layerStillOnHostScreen: false)
                .Should().BeFalse();
        }

        // The ordinary path must stay ordinary: with nothing recorded there is nothing to refuse and nothing to
        // clean up, so the overwhelming majority of opens pay nothing for any of this.
        [Test]
        public void GIVEN_no_window_recorded_WHEN_opening_THEN_it_neither_refuses_nor_cleans()
        {
            ChatWindowLivenessPolicy.ShouldRefuseOpen(false, false).Should().BeFalse();
            ChatWindowLivenessPolicy.IsGhost(false, false).Should().BeFalse();
        }

        // The two answers must never both be true: one says "leave it alone", the other says "tear it down", and
        // a state that claimed both would send the caller down both paths in the same breath.
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void GIVEN_any_state_WHEN_asked_THEN_refusing_and_ghosting_are_never_both_true(bool recorded, bool onScreen)
        {
            (ChatWindowLivenessPolicy.ShouldRefuseOpen(recorded, onScreen)
             && ChatWindowLivenessPolicy.IsGhost(recorded, onScreen)).Should().BeFalse();
        }
    }
}
