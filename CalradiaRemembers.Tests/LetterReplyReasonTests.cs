// Code written by Gabriel Mailhot, 08/10/2026.
// What a character's reply to the player's letter is about. IdentityCrysys (Nexus, 08/10/2026): "I think I just
// acknowledged the first letter and replied to every other ... she keeps mentioning the player's silence in every
// letter", with memories that read "Player wrote to me, and I replied (to ask why no reply had come)" day after day. A
// reply took the occasion of the letter it answered, so once the player answered her "why no reply?" follow-up, every
// reply after it was another "why no reply?": the player had written, and she wrote of his silence. A reply to that
// follow-up goes on with what the thread is about; a follow-up is only ever sent for a romantic letter left unanswered.
// If this breaks, a correspondent reproaches the player for a silence in answer to the player's own letter.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LetterReplyReasonTests
   {
      // IdentityCrysys's case: the player answered the "why no reply?" letter. Her answer is a love letter, not a reproach.
      [Test]
      public void GIVEN_the_player_answered_a_why_no_reply_letter_WHEN_she_replies_THEN_her_reply_is_about_their_correspondence()
      {
         LetterReplyReason.Of(LetterReason.AwaitingReply).Should().Be(LetterReason.RomanticCorrespondence);
      }

      // STAKE: answering a love letter does not draw a second confession; the reply is their correspondence (23/11/2026).
      [Test]
      public void GIVEN_the_player_answered_a_love_letter_WHEN_they_reply_THEN_the_reply_is_their_correspondence()
      {
         LetterReplyReason.Of(LetterReason.LoveConfession).Should().Be(LetterReason.RomanticCorrespondence);
      }

      // Every other occasion carries on as it was: a reply to a proposal is about the proposal.
      [Test]
      public void GIVEN_any_other_letter_answered_WHEN_replied_to_THEN_the_occasion_carries_on()
      {
         LetterReplyReason.Of(LetterReason.MarriageProposal).Should().Be(LetterReason.MarriageProposal);
         LetterReplyReason.Of(LetterReason.RomanticCorrespondence).Should().Be(LetterReason.RomanticCorrespondence);
      }
   }
}
