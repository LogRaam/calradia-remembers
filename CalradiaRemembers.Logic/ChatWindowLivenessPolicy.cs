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
// This is the rule that tells a live window from a ghost. It is two lines of arithmetic on purpose: the value is
// not the cleverness, it is that "already open" can never again mean "was open once, hours ago".

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether a recorded chat window is genuinely live, or a leftover that must not block the next one.</summary>
    public static class ChatWindowLivenessPolicy
    {
        /// <summary>
        ///   True when a window is recorded as open but its layer is no longer attached to the screen it was
        ///   opened on: a ghost. The caller must clear it and carry on rather than refuse, because refusing is
        ///   what costs the player every conversation until they restart the game.
        /// </summary>
        public static bool IsGhost(bool hasRecordedLayer, bool layerStillOnHostScreen)
            => hasRecordedLayer && !layerStillOnHostScreen;

        /// <summary>
        ///   True when an open request must be refused: a window is recorded AND it is genuinely still there.
        ///   The only case where refusing is right, since a second window over a live one would strand both.
        /// </summary>
        public static bool ShouldRefuseOpen(bool hasRecordedLayer, bool layerStillOnHostScreen)
            => hasRecordedLayer && layerStillOnHostScreen;
    }
}
