// Code written by Gabriel Mailhot, 17/07/2026.
// Divorce, Phase 1: the PAIR lock (romance audit M-D3). A divorce process must be locked by the couple, not the
// requester alone. Persuading spouse A then spouse B of the SAME marriage on one day means two DIFFERENT
// requesters, so a requester-only lock lets the second through; that opened two crossed processes, the second
// resolving on a false "household closed ranks" and docking the player's relation for nothing. PairAlreadyEngaged
// is the pure rule the host now gates Create on; these tests pin that either party, in either slot, is caught.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DivorceProcessTests
   {
      private static DivorceProcess Active(string npcId, string spouseId)
         => new() {NpcId = npcId, SpouseId = spouseId, State = DivorceProcessState.Active};

      // The exact reported double-process: A is estranging B (A is the requester). Persuading B next has B as the
      // requester, a DIFFERENT NpcId, so a requester-only lock misses it; the pair lock must catch B as the spouse
      // of the live process.
      [Test]
      public void GIVEN_a_process_where_A_estranges_B_WHEN_asking_about_B_as_the_new_requester_THEN_the_pair_is_engaged()
      {
         var active = new List<DivorceProcess> {Active("A", "B")};

         DivorceProcess.PairAlreadyEngaged(active, "B", "A").Should().BeTrue();
      }

      // The requester side is also locked, so re-persuading the SAME requester cannot stack a second process.
      [Test]
      public void GIVEN_a_process_where_A_estranges_B_WHEN_asking_about_A_again_THEN_the_pair_is_engaged()
      {
         var active = new List<DivorceProcess> {Active("A", "B")};

         DivorceProcess.PairAlreadyEngaged(active, "A", "B").Should().BeTrue();
      }

      // An unrelated couple must NOT be locked, or a real, separate divorce could never begin while any other is
      // in flight.
      [Test]
      public void GIVEN_a_process_for_A_and_B_WHEN_asking_about_an_unrelated_pair_THEN_it_is_free()
      {
         var active = new List<DivorceProcess> {Active("A", "B")};

         DivorceProcess.PairAlreadyEngaged(active, "C", "D").Should().BeFalse();
      }

      // A terminal (Fulfilled/Aborted) process is not a live lock: a party to it must be free to start a new
      // process once the old one has ended, or a second marriage could never be dissolved.
      [Test]
      public void GIVEN_only_a_terminal_process_WHEN_asking_about_its_parties_THEN_they_are_free()
      {
         var active = new List<DivorceProcess> {
            new() {NpcId = "A", SpouseId = "B", State = DivorceProcessState.Fulfilled}
         };

         DivorceProcess.PairAlreadyEngaged(active, "A", "B").Should().BeFalse();
      }

      // A null or empty list is free (no processes, no lock) and must never throw.
      [Test]
      public void GIVEN_no_active_processes_WHEN_asking_THEN_it_is_free_and_does_not_throw()
      {
         DivorceProcess.PairAlreadyEngaged(null, "A", "B").Should().BeFalse();
         DivorceProcess.PairAlreadyEngaged(new List<DivorceProcess>(), "A", "B").Should().BeFalse();
      }
   }
}
