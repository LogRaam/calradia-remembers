// Code written by Gabriel Mailhot, 24/11/2026.
// What this pins: a release granted in chat is announced only once the game has really let the prisoner go.
//
// whiskeypatt (Discord, 24/11/2026): "I granted a prisoner freedom in return for something. The option of y/n appeared
// and i put y. A message popped up saying 'the person is now free'. However when i ended the conversation they were
// still my prisoner." The success line was written right after the release was asked for, with no look at whether it
// had happened. If these fail, a player reads "set free" about someone still in his prison, and a ransom or an oath is
// collected for a freedom that was never given.

#region

using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PrisonerReleaseCheckTests
   {
      // THE REPORT: the game still holds the prisoner after the release. The player must be told so, plainly, and told
      // that nothing of the bargain moved, rather than reading that the person is free.
      [Test]
      public void GIVEN_the_prisoner_is_still_held_after_the_release_WHEN_checked_THEN_the_player_is_told_and_the_bargain_stops()
      {
         string refusal = PrisonerReleaseCheck.RefusalWhenStillHeld(true, "Rhagaea");

         refusal.Should().Contain("Rhagaea");
         refusal.Should().Contain("still held");
         refusal.Should().Contain("Nothing of the bargain was carried out");
      }

      // A release that took must go on exactly as before: the announcement, the regard, the terms. A refusal here would
      // take back every freedom the player grants.
      [Test]
      public void GIVEN_the_prisoner_is_free_after_the_release_WHEN_checked_THEN_nothing_stands_in_the_way()
      {
         PrisonerReleaseCheck.RefusalWhenStillHeld(false, "Rhagaea").Should().BeNull();
      }

      // A captive the game names badly (a synthesized one) must still get a sentence the player can read.
      [Test]
      public void GIVEN_a_prisoner_with_no_name_WHEN_still_held_THEN_the_refusal_still_reads_as_a_sentence()
      {
         PrisonerReleaseCheck.RefusalWhenStillHeld(true, " ").Should().StartWith("(The prisoner could not be set free");
      }
   }
}
