// Code written by Gabriel Mailhot, 08/10/2026.
// The game's own quests, remembered by the one who set them. tashmetu and Maifailun (Nexus, 08/10/2026): accepting a
// native quest closes the conversation in the game's own dialogue, and nothing of it was kept: the lord who asked for
// it, spoken to later, only knew it while it was still open, and nothing at all once it was done or failed ("Maifailun
// would love quest memories from it"). Gabriel ratified a memory for the giver when such a quest begins and when it
// ends, worded by how it ended. If this breaks, a lord forgets the errand he set, or thanks the player for one failed.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NativeQuestMemoryTests
   {
      // The giver remembers asking: an agreement between them.
      [Test]
      public void GIVEN_a_native_quest_accepted_WHEN_remembered_THEN_the_giver_recalls_asking_for_it()
      {
         NativeQuestMemory m = NativeQuestMemory.For(NativeQuestPhase.Started, "Can", "Escort the merchant caravan");

         m.Kind.Should().Be(NotableEventType.Agreement);
         m.Summary.Should().Contain("Can").And.Contain("Escort the merchant caravan");
      }

      // Done: a deed done together. Failed or let lapse: remembered as such. Betrayed: a betrayal.
      [Test]
      public void GIVEN_a_native_quest_ended_WHEN_remembered_THEN_the_memory_follows_how_it_ended()
      {
         NativeQuestMemory.For(NativeQuestPhase.Succeeded, "Can", "t").Kind.Should().Be(NotableEventType.Collaboration);
         NativeQuestMemory.For(NativeQuestPhase.Failed, "Can", "t").Summary.Should().Contain("failed");
         NativeQuestMemory.For(NativeQuestPhase.TimedOut, "Can", "t").Summary.Should().Contain("ran out");
         NativeQuestMemory.For(NativeQuestPhase.Betrayed, "Can", "t").Kind.Should().Be(NotableEventType.Betrayal);
         NativeQuestMemory.For(NativeQuestPhase.Cancelled, "Can", "t").Summary.Should().Contain("came to nothing");
      }

      // A quest the game voids leaves nothing: it says nothing about what the player did.
      [Test]
      public void GIVEN_a_quest_the_game_voided_WHEN_remembered_THEN_nothing_is_written()
      {
         NativeQuestMemory.For(NativeQuestPhase.Invalid, "Can", "t").Should().BeNull();
         NativeQuestMemory.For(NativeQuestPhase.Started, "Can", "  ").Should().BeNull();
      }
   }
}
