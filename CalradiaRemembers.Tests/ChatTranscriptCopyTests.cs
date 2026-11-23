// Code written by Gabriel Mailhot, 22/11/2026.
// The conversation copied to the clipboard with Ctrl+Shift+C. whiskeypatt (Discord 21/11/2026): "I cannot copy what they
// wrote (UI wont let me)". If this is wrong, the pasted text carries the window's markup, loses who said what, or the
// shortcut steals the input field's own Ctrl+C.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChatTranscriptCopyTests
   {
      // STAKE: the pasted text says who spoke each line, as the window does.
      [Test]
      public void GIVEN_two_messages_WHEN_copied_THEN_each_reads_author_colon_text_with_a_blank_line_between()
      {
         ChatTranscriptCopy.Compose(new[] {"Hex\nGood day, my lord.", "Count Aldric\nGood day to you."})
                           .Should().Be("Hex: Good day, my lord.\r\n\r\nCount Aldric: Good day to you.");
      }

      // STAKE: the window's colour markup never lands in a forum post or a bug report.
      [Test]
      public void GIVEN_rich_text_tags_WHEN_copied_THEN_they_are_removed()
      {
         ChatTranscriptCopy.Compose(new[] {"Count Aldric\n<span style=\"Narration\">He sets down the cup.</span> Hold it?"})
                           .Should().Be("Count Aldric: He sets down the cup. Hold it?");
      }

      // STAKE: a system line with no author, and empty blocks, do not break the text.
      [Test]
      public void GIVEN_a_system_line_and_empty_blocks_WHEN_copied_THEN_the_line_stays_and_empties_are_skipped()
      {
         ChatTranscriptCopy.Compose(new[] {"", "The conversation begins.", null}).Should().Be("The conversation begins.");
         ChatTranscriptCopy.Compose(null).Should().BeEmpty();
      }

      // STAKE: Ctrl+C alone stays the input field's own copy; only Ctrl+Shift+C copies the conversation.
      [Test]
      public void GIVEN_the_keys_WHEN_read_THEN_only_ctrl_shift_c_is_the_shortcut()
      {
         ChatTranscriptCopy.IsShortcut(new ChatCopyKeys {Control = true, Shift = true, CPressed = true}).Should().BeTrue();
         ChatTranscriptCopy.IsShortcut(new ChatCopyKeys {Control = true, CPressed = true}).Should().BeFalse();
         ChatTranscriptCopy.IsShortcut(new ChatCopyKeys {Shift = true, CPressed = true}).Should().BeFalse();
         ChatTranscriptCopy.IsShortcut(new ChatCopyKeys {Control = true, Shift = true}).Should().BeFalse();
      }
   }
}
