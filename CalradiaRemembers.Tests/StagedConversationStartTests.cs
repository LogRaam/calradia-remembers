// Code written by Gabriel Mailhot, 08/10/2026.
// When a staged scene starts the game's conversation itself. Gabriel's play test (08/10/2026): the post-duel scene opened
// in the town square with Tharos brought beside the player, but the game's conversation with him never began within the
// second the stager waits, so the chat opened over the scene with no conversation: Tharos, held by nothing, walked off,
// and the game's own greeting came ten seconds later instead of the post-duel talk. When the scene is up and the person
// stands in it but no conversation has begun, the stager begins it itself, once, as the game does when one clicks on
// someone; only if that fails does the bare fallback open the chat.
// If this breaks, a staged conversation opens with the person walking away from it.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class StagedConversationStartTests
   {
      private static StagedStartFacts Square() => new() {
         WaitingOnAnInteriorScene = true, ConversationLive = false, PartnerInTheScene = true, AlreadyTried = false
      };

      // Gabriel's Tharos: the scene is up, he stands in it, nothing has begun. The stager begins it.
      [Test]
      public void GIVEN_the_person_in_the_scene_and_no_conversation_WHEN_the_grace_runs_out_THEN_the_stager_begins_it()
      {
         StagingGracePolicy.ShouldStartTheConversation(Square()).Should().BeTrue();
      }

      // Once only, never over a conversation already live, never for someone not there, never for a map conversation.
      [Test]
      public void GIVEN_a_conversation_live_a_try_made_no_one_there_or_a_map_conversation_WHEN_asked_THEN_it_does_not()
      {
         StagingGracePolicy.ShouldStartTheConversation(new StagedStartFacts {WaitingOnAnInteriorScene = true, ConversationLive = true, PartnerInTheScene = true}).Should().BeFalse();
         StagingGracePolicy.ShouldStartTheConversation(new StagedStartFacts {WaitingOnAnInteriorScene = true, PartnerInTheScene = true, AlreadyTried = true}).Should().BeFalse();
         StagingGracePolicy.ShouldStartTheConversation(new StagedStartFacts {WaitingOnAnInteriorScene = true, PartnerInTheScene = false}).Should().BeFalse();
         StagingGracePolicy.ShouldStartTheConversation(new StagedStartFacts {WaitingOnAnInteriorScene = false, PartnerInTheScene = true}).Should().BeFalse();
      }
   }
}
