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

        private int _scrollToBottomFramesRemaining;

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
    }
}