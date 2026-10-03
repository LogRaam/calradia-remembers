// Code written by Gabriel Mailhot, 03/10/2026.
// EmperorHaterius (Nexus, 03/10/2026): "Is there a hotkey to exit CR conversations? I can't get out of them." The
// close button was the only way out, and Escape did nothing. Escape now leaves the conversation as the button does,
// one layer at a time: a screen over the chat keeps its own Escape, an open panel closes first, a message being typed
// is left (draft kept) before the conversation closes.

namespace CalradiaRemembers.Logic
{
   /// <summary>What the chat should do with one press of Escape.</summary>
   public enum ChatEscapeAction
   {
      /// <summary>Not the chat's key to take: another screen is over it.</summary>
      Ignore,

      /// <summary>Close the open side panel; the conversation stays.</summary>
      ClosePanel,

      /// <summary>Leave the message field, keeping what was typed.</summary>
      StopTyping,

      /// <summary>Leave the conversation, as the close button does.</summary>
      CloseChat
   }

   /// <summary>The state an Escape press is judged on.</summary>
   public sealed class ChatEscapeFacts
   {
      /// <summary>The chat holds the keyboard (no menu, encyclopedia or popup over it).</summary>
      public bool ChatHasFocus { get; init; }

      /// <summary>A side panel is open (memories, portrait, compose, actions, the council dossier).</summary>
      public bool PanelOpen { get; init; }

      /// <summary>The message field has the keyboard and holds text.</summary>
      public bool TypingWithText { get; init; }
   }

   /// <summary>Decides what one Escape press does in the chat. Pure.</summary>
   public static class ChatEscapePolicy
   {
      /// <summary>Outermost layer first.</summary>
      public static ChatEscapeAction Decide(ChatEscapeFacts f)
      {
         if (f == null || !f.ChatHasFocus) return ChatEscapeAction.Ignore;
         if (f.PanelOpen) return ChatEscapeAction.ClosePanel;

         return f.TypingWithText ? ChatEscapeAction.StopTyping : ChatEscapeAction.CloseChat;
      }
   }
}
