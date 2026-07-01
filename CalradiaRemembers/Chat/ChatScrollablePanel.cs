// Code written by Gabriel Mailhot, 21/05/2026.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   Custom ScrollablePanel that supports a deferred "scroll to bottom"
    ///   request. The ViewModel sets ScrollToBottomRequested = true after adding
    ///   a message; on the next late update (after the base class has recomputed
    ///   MaxValue for the new content), we snap the vertical scrollbar to the
    ///   bottom.
    ///   Self-registers into ActiveInstance on construction so ChatManager can
    ///   reach us directly without traversing the widget tree (GauntletLayer
    ///   does not expose Root in this Bannerlord version).
    /// </summary>
    public class ChatScrollablePanel : ScrollablePanel
    {
        // Within this many scroll units of the bottom, the player counts as "following" the latest line.
        private const float PinThreshold = 40f;

        // Only reveal the "scroll down" hint once there is a SUBSTANTIAL block of unread text below the fold —
        // not a sliver. MaxValue is the scroll RANGE (0 when content fits), so a message that overflows the
        // viewport by a single line gives a tiny range; the old 40-unit gate fired on that and the hint flashed
        // up the instant a scene's opening narration ran long. Require roughly a few lines' worth hidden below.
        private const float MoreHintRevealMargin = 160f;

        // Id of the sibling hint widget toggled when there is unread text below the fold (see the chat prefab).
        private const string HintWidgetId = "ScrollMoreHint";

        private int _scrollToBottomFramesRemaining;
        private Widget _moreHint;

        public ChatScrollablePanel(UIContext context) : base(context)
        {
            ActiveInstance = this;
            ModLog.Info("ChatScrollablePanel: instance created and registered.");
        }


        public static ChatScrollablePanel ActiveInstance { get; private set; }


        /// <summary>
        ///   Follow new content to the bottom — but ONLY when the player was already at (or near) the
        ///   bottom. If they have scrolled up to read, leave their position alone: a long, multi-reply
        ///   answer must not keep yanking the scrollbar back down while they are still reading. The check
        ///   reads the CURRENT MaxValue, before the new line has laid out and grown it.
        /// </summary>
        public void RequestScrollToBottom()
        {
            if (WasFollowingBottom())
                _scrollToBottomFramesRemaining = 8;
        }

        private bool WasFollowingBottom()
        {
            if (VerticalScrollbar == null) return true; // nothing to fight yet

            float max = VerticalScrollbar.MaxValue;

            return max <= 0f || VerticalScrollbar.ValueFloat >= max - PinThreshold;
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);

            // Run LLM-turn continuations on the main thread. The chat overlay pauses campaign
            // time, so this per-frame UI tick is the only reliable main-thread pump.
            // WARNING: every LLM reply is delivered through this drain. If this widget is ever
            // removed from the chat prefab (or stops ticking), responses will HANG, not error —
            // keep a pump here, or move it to another per-frame main-thread hook.
            MainThreadDispatcher.Pump();

            UpdateMoreHint();

            if (_scrollToBottomFramesRemaining <= 0)
                return;

            if (VerticalScrollbar == null)
            {
                _scrollToBottomFramesRemaining--;

                return;
            }

            VerticalScrollbar.ValueFloat = VerticalScrollbar.MaxValue;
            _scrollToBottomFramesRemaining--;
        }

        /// <summary>
        ///   Shows the sibling "scroll down to read more" hint only while the player is scrolled up with unread
        ///   text below — i.e. the scrollbar can move and is not pinned near the bottom. Hidden when following the
        ///   latest line or when there is nothing to scroll. Fail-soft: no hint widget / no scrollbar, do nothing.
        /// </summary>
        private void UpdateMoreHint()
        {
            Widget hint = ResolveMoreHint();
            if (hint == null || VerticalScrollbar == null) return;

            // Show only when a meaningful amount of text sits below the current view: the unread span
            // (MaxValue - ValueFloat) must exceed the reveal margin. This also implies the content genuinely
            // overflows, so a near-fitting message never trips it, and following the latest line hides it.
            float unreadBelow = VerticalScrollbar.MaxValue - VerticalScrollbar.ValueFloat;
            bool moreBelow = unreadBelow > MoreHintRevealMargin;

            if (hint.IsVisible != moreBelow) hint.IsVisible = moreBelow;
        }

        private Widget ResolveMoreHint()
        {
            if (_moreHint != null) return _moreHint;
            if (ParentWidget == null) return null;

            foreach (Widget child in ParentWidget.Children)
                if (child.Id == HintWidgetId) { _moreHint = child; break; }

            return _moreHint;
        }
    }
}