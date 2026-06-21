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
        private int _scrollToBottomFramesRemaining;

        public ChatScrollablePanel(UIContext context) : base(context)
        {
            ActiveInstance = this;
            ModLog.Info("ChatScrollablePanel: instance created and registered.");
        }


        public static ChatScrollablePanel ActiveInstance { get; private set; }


        public void RequestScrollToBottom()
        {
            _scrollToBottomFramesRemaining = 8;
        }

        protected override void OnLateUpdate(float dt)
        {
            // A deferred close tears down this very widget. Hand it to ChatManager's deferred path,
            // which (on the map) destroys the layer on a NATIVE campaign tick — OUTSIDE this UI
            // LateUpdate — so we never remove our own layer mid-update (which NREs in GauntletUI's
            // DefragContainers next frame: the letter-reply crash).
            if (MainThreadDispatcher.ConsumeCloseRequest())
            {
                ChatManager.RequestDeferredClose();
                return;
            }

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