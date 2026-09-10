// Code written by Gabriel Mailhot, 08/09/2026.
// Player report (Nexus, 2026-09-08, v2.5.3): a companion approached in a village, no CR chat window appeared,
// clicking "a word with you" just ended the conversation, "and then after that, no CR chat windows with anyone
// work unless I fully quit and restart the game". Seen twice.
//
// The last sentence is the whole bug. ChatManager refuses to open when one is already open, and it decides that
// from a single field holding the live layer. Close() is written with real care (everything guarded, the reset
// guaranteed in a finally), so the trap is not a close that failed: it is an open whose close never came, leaving
// a layer recorded against a screen that has since gone. From then on every entry no-ops in silence and the
// vanilla dialogue, with nowhere to route, simply ends. One bad moment costs the player the feature for the rest
// of the session.
//
// This is the rule that tells a live window from a ghost. It is a few lines of arithmetic on purpose: the value is
// not the cleverness, it is that "already open" can never again mean "was open once, hours ago".
//
// EXTENDED 2026-09-09 after a second report of the same shape (tashmetu, v2.5.6) proved the first rule too narrow.
// It asked only whether the layer was still in its host screen's layer list, and a screen the engine has already
// finalized STILL HOLDS that list: a chat stranded on a mission that has since ended therefore read as perfectly
// live, and refused every conversation for the rest of the session exactly as before. A window is only live if the
// screen holding it is live too, so the host's own liveness is now part of the question.

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether a recorded chat window is genuinely live, or a leftover that must not block the next one.</summary>
    public static class ChatWindowLivenessPolicy
    {
        /// <summary>
        ///   True when a window is recorded as open but is not really there any more: its layer has left the
        ///   screen it was opened on, OR that screen has itself been torn down while still holding the layer.
        ///   Either way it is a ghost, and the caller must clear it and carry on rather than refuse, because
        ///   refusing is what costs the player every conversation until they restart the game.
        /// </summary>
        public static bool IsGhost(bool hasRecordedLayer, bool layerStillOnHostScreen, bool hostScreenAlive)
            => hasRecordedLayer && !IsGenuinelyLive(layerStillOnHostScreen, hostScreenAlive);

        /// <summary>
        ///   True when an open request must be refused: a window is recorded AND it is genuinely still there, on
        ///   a screen that is still alive. The only case where refusing is right, since a second window over a
        ///   live one would strand both.
        /// </summary>
        public static bool ShouldRefuseOpen(bool hasRecordedLayer, bool layerStillOnHostScreen, bool hostScreenAlive)
            => hasRecordedLayer && IsGenuinelyLive(layerStillOnHostScreen, hostScreenAlive);

        /// <summary>Both halves of "really there", kept in one place so the two answers can never disagree.</summary>
        private static bool IsGenuinelyLive(bool layerStillOnHostScreen, bool hostScreenAlive)
            => layerStillOnHostScreen && hostScreenAlive;
    }
}
