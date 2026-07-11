// Code written by Gabriel Mailhot, 11/07/2026.

#region

using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   The chat's quick-input field. One behaviour on top of a plain <see cref="EditableTextWidget" />: while
    ///   it holds focus, pressing Enter (or the numpad Enter) sends the message, exactly as clicking Send would
    ///   (player request). The send guard lives in <see cref="ChatViewModel.ExecuteSendMessage" /> (empty text or
    ///   a turn already in flight is ignored), so this only forwards the keystroke. The multi-line compose panel
    ///   on the right is a different widget and deliberately keeps Enter as a newline for stage directions.
    /// </summary>
    public class ChatInputWidget : EditableTextWidget
    {
        private bool _focused;

        public ChatInputWidget(UIContext context) : base(context)
        {
        }

        protected override void OnGainFocus()
        {
            base.OnGainFocus();
            _focused = true;
        }

        protected override void OnLoseFocus()
        {
            base.OnLoseFocus();
            _focused = false;
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);

            if (!_focused) return;
            if (!Input.IsKeyPressed(InputKey.Enter) && !Input.IsKeyPressed(InputKey.NumpadEnter)) return;

            ChatManager.ActiveViewModel?.ExecuteSendMessage();
        }
    }
}
