// Code written by Gabriel Mailhot, 04/11/2026.
// CompanionGrievancePolicy decides what an unhappy companion is told about WHY they are unhappy. froggyluv (Nexus,
// 04/11/2026): companions "hallucinating some broken promise they swear happened... things that never happened". The note
// said "voice your grievance: what has soured you" and named nothing, so the model invented a cause. If this is wrong, a
// companion accuses the player of a wrong that never happened, or forgets the real one the game counted.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Companions
{
   [TestFixture]
   public sealed class CompanionGrievanceTests
   {
      // STAKE: froggyluv: a disgruntled companion with no recorded wrong must not be invited to name one.
      [Test]
      public void GIVEN_a_disgruntled_companion_with_no_recorded_cause_WHEN_the_note_is_built_THEN_it_says_no_single_wrong_and_forbids_inventing_one()
      {
         string note = CompanionGrievancePolicy.MoodNote(20, new List<GrievanceEntry>());

         note.Should().Contain("No single wrong");
         note.Should().Contain("Never claim a broken promise");
      }

      // STAKE: the real cause the game counted is what the companion speaks of, so the grievance is one the player can mend.
      [Test]
      public void GIVEN_a_companion_left_idle_and_refused_a_hearing_WHEN_the_note_is_built_THEN_it_names_both_heaviest_first()
      {
         var recent = new List<GrievanceEntry> {
            new GrievanceEntry {Day = 10, Driver = HappinessDriver.Idle},
            new GrievanceEntry {Day = 12, Driver = HappinessDriver.Mistreated, Cause = "being refused a hearing when you asked the player for a word"}
         };

         string note = CompanionGrievancePolicy.MoodNote(20, recent);

         note.Should().Contain("refused a hearing");
         note.Should().Contain("left idle");
         note.IndexOf("refused a hearing").Should().BeLessThan(note.IndexOf("left idle"));
      }

      // STAKE: a content companion carries no note at all, so nothing sours a happy one.
      [Test]
      public void GIVEN_a_content_companion_WHEN_the_note_is_built_THEN_there_is_none()
      {
         CompanionGrievancePolicy.MoodNote(60, new List<GrievanceEntry> {new GrievanceEntry {Driver = HappinessDriver.Idle}}).Should().BeNull();
      }

      // STAKE: only what lowered contentment is kept; a victory is no grievance, and old wrongs the weekly drift has eased fall away.
      [Test]
      public void GIVEN_a_ledger_WHEN_a_lift_and_an_old_wrong_are_recorded_THEN_only_recent_wrongs_are_read()
      {
         var ledger = new CompanionGrievanceLedger();
         ledger.Record("c1", new GrievanceEntry {Day = 1, Driver = HappinessDriver.Captured});
         ledger.Record("c1", new GrievanceEntry {Day = 100, Driver = HappinessDriver.SharedVictory});
         ledger.Record("c1", new GrievanceEntry {Day = 100, Driver = HappinessDriver.ErrandFailed});

         IReadOnlyList<GrievanceEntry> recent = ledger.Recent("c1", 100);

         recent.Should().ContainSingle().Which.Driver.Should().Be(HappinessDriver.ErrandFailed);
      }

      // STAKE: the causes survive a save and load; a save written before this reads an empty ledger, not a crash.
      [Test]
      public void GIVEN_a_saved_ledger_WHEN_read_back_THEN_it_keeps_its_causes_and_an_old_save_reads_empty()
      {
         var ledger = new CompanionGrievanceLedger();
         ledger.Record("c1", new GrievanceEntry {Day = 5, Driver = HappinessDriver.Mistreated, Cause = "a hearing refused"});

         CompanionGrievanceLedger back = CompanionGrievanceLedger.Deserialize(ledger.Serialize());

         back.Recent("c1", 5).Should().ContainSingle().Which.Cause.Should().Be("a hearing refused");
         CompanionGrievanceLedger.Deserialize(null).ByCompanion.Should().BeEmpty();
         CompanionGrievanceLedger.Deserialize("{broken").ByCompanion.Should().BeEmpty();
      }

      // STAKE: a long unhappy stretch keeps only the latest causes, so the save does not grow without end.
      [Test]
      public void GIVEN_many_wrongs_WHEN_recorded_THEN_only_the_latest_are_kept()
      {
         var ledger = new CompanionGrievanceLedger();
         for (int d = 0; d < 20; d++) ledger.Record("c1", new GrievanceEntry {Day = d, Driver = HappinessDriver.Idle});

         ledger.ByCompanion["c1"].Should().HaveCount(CompanionGrievanceLedger.MaxKept);
         ledger.ByCompanion["c1"][0].Day.Should().Be(20 - CompanionGrievanceLedger.MaxKept);
      }
   }
}
