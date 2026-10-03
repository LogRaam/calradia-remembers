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

        // The field holding the keyboard, so Escape can leave it before it closes the conversation (03/10/2026).
        private static ChatInputWidget _typingIn;

        /// <summary>True while the message field holds the keyboard.</summary>
        internal static bool IsTyping => _typingIn != null;

        /// <summary>Leaves the message field, keeping what was typed.</summary>
        internal static void StopTyping() => _typingIn?.EventManager?.ClearFocus();

        public ChatInputWidget(UIContext context) : base(context)
        {
        }

        protected override void OnGainFocus()
        {
            base.OnGainFocus();
            _focused = true;
            _typingIn = this;
        }

        protected override void OnLoseFocus()
        {
            base.OnLoseFocus();
            _focused = false;
            if (ReferenceEquals(_typingIn, this)) _typingIn = null;
        }

        // A chat closed while the field held the keyboard never loses focus first: drop the reference with the widget.
        protected override void OnDisconnectedFromRoot()
        {
            base.OnDisconnectedFromRoot();
            if (ReferenceEquals(_typingIn, this)) _typingIn = null;
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
