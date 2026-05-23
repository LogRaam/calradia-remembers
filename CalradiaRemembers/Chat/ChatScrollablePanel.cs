using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace CalradiaRemembers.Chat
{
    /// <summary>
    /// Custom ScrollablePanel that supports a deferred "scroll to bottom"
    /// request. The ViewModel sets ScrollToBottomRequested = true after adding
    /// a message; on the next late update (after the base class has recomputed
    /// MaxValue for the new content), we snap the vertical scrollbar to the
    /// bottom.
    ///
    /// Self-registers into ActiveInstance on construction so ChatManager can
    /// reach us directly without traversing the widget tree (GauntletLayer
    /// does not expose Root in this Bannerlord version).
    /// </summary>
    public class ChatScrollablePanel : ScrollablePanel
    {
        public static ChatScrollablePanel ActiveInstance { get; private set; }

        public bool ScrollToBottomRequested { get; set; }

        public ChatScrollablePanel(UIContext context) : base(context)
        {
            ActiveInstance = this;
            ModLog.Info("ChatScrollablePanel: instance created and registered.");
        }

        protected override void OnLateUpdate(float dt)
        {
            // base.OnLateUpdate runs UpdateScrollablePanel which (re)computes
            // VerticalScrollbar.MaxValue based on the current inner panel size.
            base.OnLateUpdate(dt);

            if (!ScrollToBottomRequested) return;
            if (VerticalScrollbar == null)
            {
                ModLog.Info("ChatScrollablePanel.OnLateUpdate: requested but VerticalScrollbar is null");
                return;
            }

            float before = VerticalScrollbar.ValueFloat;
            float target = VerticalScrollbar.MaxValue;
            VerticalScrollbar.ValueFloat = target;
            ScrollToBottomRequested = false;

            ModLog.Info($"ChatScrollablePanel.OnLateUpdate: scrolled from {before} to {target}");
        }
    }
}
