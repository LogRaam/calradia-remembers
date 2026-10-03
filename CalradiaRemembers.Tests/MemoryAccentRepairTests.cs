// Code written by Gabriel Mailhot, 03/10/2026.
// Shinelegacy (Nexus, 03/10/2026): memories typed at the console before the fix were stored as mojibake ("Ã©" for an
// e-acute). Gabriel: "est-ce que c'est possible de faire une commande console qui corrige toute la memoire?" The
// command repairs every stored memory in one pass. If this rule is wrong, a mangled memory stays mangled, a memory
// that was fine is altered, or a repaired memory loses what it carried besides its words (private, its deed tag).

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MemoryAccentRepairTests
   {
      private static NpcProfile WithMemories(params NotableEvent[] events) => new() {Id = "liena", Name = "Liena", Clan = "", Faction = "", Events = new List<NotableEvent>(events)};

      // The mangled memory reads as typed; the one that was fine is untouched; the count says how many were mended.
      [Test]
      public void GIVEN_one_mangled_memory_and_one_intact_WHEN_repaired_THEN_only_the_mangled_one_changes()
      {
         NpcProfile p = WithMemories(new NotableEvent(3, NotableEventType.Other, ConsoleTextRepair.Mangle("Elle m'a aidé à Pravend.")),
                                     new NotableEvent(5, NotableEventType.Other, "Nous étions déjà amis."));

         int mended = MemoryAccentRepair.Repair(p);

         mended.Should().Be(1);
         p.Events[0].summary.Should().Be("Elle m'a aidé à Pravend.");
         p.Events[1].summary.Should().Be("Nous étions déjà amis.");
      }

      // A repaired memory keeps everything but its words: kept private, its deed tag, its day.
      [Test]
      public void GIVEN_a_private_tagged_memory_WHEN_repaired_THEN_it_stays_private_tagged_and_dated()
      {
         NpcProfile p = WithMemories(new NotableEvent(7, NotableEventType.Other, ConsoleTextRepair.Mangle("Un secret gardé.")) {IsPrivate = true, ActionTag = "Gave gold"});

         MemoryAccentRepair.Repair(p);

         p.Events[0].IsPrivate.Should().BeTrue();
         p.Events[0].ActionTag.Should().Be("Gave gold");
         p.Events[0].gameDay.Should().Be(7);
      }

      // A profile with nothing to mend reports nothing, so the command's tally counts only real repairs.
      [Test]
      public void GIVEN_no_mangled_memory_WHEN_repaired_THEN_nothing_is_counted()
      {
         MemoryAccentRepair.Repair(WithMemories(new NotableEvent(1, NotableEventType.Other, "We met at Pendraic."))).Should().Be(0);
         MemoryAccentRepair.Repair(WithMemories()).Should().Be(0);
      }
   }
}
