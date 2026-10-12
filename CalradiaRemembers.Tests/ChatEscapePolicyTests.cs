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

      // philip0105 (Nexus, 11/10/2026): "game will crash if you leave a conversation with Esc key instead of the 'End
      // conversation' button". The close ran inside the chat's own per-frame widget update, while the same Escape press
      // was still live for the game underneath. It now waits: the key is let go, one more frame passes, then it closes.
      [Test]
      public void GIVEN_escape_asked_to_close_WHEN_the_key_is_still_held_THEN_the_conversation_does_not_close_yet()
      {
         var latch = new EscapeCloseLatch();
         latch.Arm();

         latch.Step(escapeDown: true).Should().BeFalse();
         latch.Step(escapeDown: true).Should().BeFalse();
      }

      // Same report: the frame the key comes up still carries its release for the game, so the close comes the frame after.
      [Test]
      public void GIVEN_escape_asked_to_close_WHEN_the_key_is_let_go_THEN_it_closes_one_frame_later_and_only_once()
      {
         var latch = new EscapeCloseLatch();
         latch.Arm();

         latch.Step(escapeDown: false).Should().BeFalse();
         latch.Step(escapeDown: false).Should().BeTrue();
         latch.Step(escapeDown: false).Should().BeFalse();
      }

      // Nothing asked, nothing closes: the latch never closes a conversation on its own.
      [Test]
      public void GIVEN_no_close_asked_WHEN_frames_pass_THEN_nothing_closes()
      {
         var latch = new EscapeCloseLatch();

         latch.Step(escapeDown: false).Should().BeFalse();
         latch.Step(escapeDown: false).Should().BeFalse();
      }

      // A half-typed message is not thrown away by a reflex: the first Escape only leaves the field, the second closes.
      [Test]
      public void GIVEN_a_message_being_typed_WHEN_escape_is_pressed_THEN_typing_stops_and_the_draft_stays()
      {
         ChatEscapePolicy.Decide(new ChatEscapeFacts {ChatHasFocus = true, TypingWithText = true}).Should().Be(ChatEscapeAction.StopTyping);
      }
   }
}
