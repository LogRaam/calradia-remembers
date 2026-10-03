// Code written by Gabriel Mailhot, 03/10/2026.
// EmperorHaterius (Nexus, 03/10/2026): "Is there a hotkey to exit CR conversations? I can't get out of them." The
// only way out of a CR conversation was its close button; Escape, the key every player tries first, did nothing. Escape
// now leaves the conversation exactly as the button does, one layer at a time. If this rule is wrong, a player whose
// button is off-screen is trapped again, an Escape meant for a menu over the chat closes the conversation behind it,
// or a half-typed message is thrown away by a reflex.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChatEscapePolicyTests
   {
      // EmperorHaterius's case: nothing else open, Escape leaves the conversation as the close button would.
      [Test]
      public void GIVEN_a_plain_conversation_WHEN_escape_is_pressed_THEN_the_conversation_closes()
      {
         ChatEscapePolicy.Decide(new ChatEscapeFacts {ChatHasFocus = true}).Should().Be(ChatEscapeAction.CloseChat);
      }

      // A menu or the encyclopedia over the chat owns that Escape: the conversation behind it must not close.
      [Test]
      public void GIVEN_another_screen_over_the_chat_WHEN_escape_is_pressed_THEN_the_chat_ignores_it()
      {
         ChatEscapePolicy.Decide(new ChatEscapeFacts {ChatHasFocus = false, PanelOpen = true, TypingWithText = true})
                         .Should().Be(ChatEscapeAction.Ignore);
      }

      // One layer at a time: an open panel (memories, portrait, compose) closes first, the conversation stays.
      [Test]
      public void GIVEN_a_panel_open_WHEN_escape_is_pressed_THEN_only_the_panel_closes()
      {
         ChatEscapePolicy.Decide(new ChatEscapeFacts {ChatHasFocus = true, PanelOpen = true, TypingWithText = true})
                         .Should().Be(ChatEscapeAction.ClosePanel);
      }

      // A half-typed message is not thrown away by a reflex: the first Escape only leaves the field, the second closes.
      [Test]
      public void GIVEN_a_message_being_typed_WHEN_escape_is_pressed_THEN_typing_stops_and_the_draft_stays()
      {
         ChatEscapePolicy.Decide(new ChatEscapeFacts {ChatHasFocus = true, TypingWithText = true}).Should().Be(ChatEscapeAction.StopTyping);
      }
   }
}
