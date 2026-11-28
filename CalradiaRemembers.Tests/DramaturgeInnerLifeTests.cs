// Code written by Gabriel Mailhot, 28/11/2026.
// The Dramaturge mode's inner life (Stage 6 of the group-scene plan), ported from the simulator's UpdateMind and LinePrompt:
// after each line a cheap reader says how the character stands now, and the next reply is written from it. The simulator's
// review of 27/11/2026: "each character must be able to be transformed by the conversation he takes part in". If this is
// wrong: a passing mood is told to the character as a change of heart, a reader's blank reply wipes what it knew, the bond
// seeded from the last meeting is lost, or an empty state writes empty lines into the prompt.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DramaturgeInnerLifeTests
   {
      private const string Reading = "feeling: wary\nlevel: tactic\nshift: She saw he lies about the toll, at his second line\naim: make him pay the toll\n"
                                     + "tactic: press him with the ledger\nshowing: polite patience\nhiding: her contempt\nvisible: a detail\nurge: high, duty\n"
                                     + "bond: Arwa: understands partly, forgives no, loves no, trusts no";

      // STAKE: the next reply is written from where the character stands now; every field the reader gave lands.
      [Test]
      public void GIVEN_a_full_reading_WHEN_applied_THEN_every_field_is_read()
      {
         DramaturgeInnerState s = DramaturgeInnerLife.Apply(new DramaturgeInnerState(), Reading);

         s.Feeling.Should().Be("wary");
         s.Level.Should().Be("tactic");
         s.Shift.Should().Be("She saw he lies about the toll, at his second line");
         s.Aim.Should().Be("make him pay the toll");
         s.Hiding.Should().Be("her contempt");
         s.Urge.Should().Be("high, duty");
         s.Bond.Should().Be("Arwa: understands partly, forgives no, loves no, trusts no");
      }

      // STAKE: the simulator's second review: "a passing mood is not a re-evaluation". A shift told at the level of a
      // fluctuation would make the character announce a change of heart for a frown.
      [Test]
      public void GIVEN_a_fluctuation_with_a_shift_WHEN_applied_THEN_no_shift_is_kept()
      {
         DramaturgeInnerState s = DramaturgeInnerLife.Apply(new DramaturgeInnerState(), "feeling: irritated\nlevel: fluctuation\nshift: he was rude\naim: be done");

         s.Shift.Should().Be("none");
         DramaturgeInnerLife.Note(s, "Arwa").Should().NotContain("what just changed");
      }

      // STAKE: a reader that answers only part of the form (or nothing usable) must not wipe what the character already
      // wanted or hid.
      [Test]
      public void GIVEN_a_partial_or_empty_reading_WHEN_applied_THEN_the_old_values_are_kept()
      {
         DramaturgeInnerState before = DramaturgeInnerLife.Apply(new DramaturgeInnerState(), Reading);

         DramaturgeInnerState partial = DramaturgeInnerLife.Apply(before, "feeling: tired");
         partial.Feeling.Should().Be("tired");
         partial.Aim.Should().Be("make him pay the toll");
         partial.Hiding.Should().Be("her contempt");

         DramaturgeInnerLife.Apply(before, "I cannot help with that.").Should().BeSameAs(before);
      }

      // STAKE: the simulator: "a tactic that has failed twice is abandoned for another"; the reader is told what was tried.
      [Test]
      public void GIVEN_a_changed_tactic_WHEN_applied_THEN_the_old_one_is_remembered_as_tried()
      {
         DramaturgeInnerState before = DramaturgeInnerLife.Apply(new DramaturgeInnerState(), Reading);

         DramaturgeInnerState after = DramaturgeInnerLife.Apply(before, "tactic: appeal to his honour");

         after.Tried.Should().Contain("press him with the ledger");
         DramaturgeInnerLife.User(new DramaturgeSheet {NpcName = "Alympia"}, after, "Arwa: hello").Should().Contain("Tactics already tried and dropped: press him with the ledger");
      }

      // STAKE: Gabriel's stage 2 memory left a bond toward the player; the conversation starts from it, not from nothing.
      [Test]
      public void GIVEN_a_meeting_bond_WHEN_seeded_THEN_it_reads_as_the_four_parts_toward_the_player()
      {
         DramaturgeInnerLife.SeedBond(new MeetingBond {Understands = "yes", Forgives = "partly", Loves = "no", Trusts = "partly"}, "Arwa")
            .Should().Be("Arwa: understands yes, forgives partly, loves no, trusts partly");
         DramaturgeInnerLife.SeedBond(null, "Arwa").Should().BeEmpty();
         DramaturgeInnerLife.SeedBond(new MeetingBond(), "Arwa").Should().BeEmpty();
      }

      // STAKE: the reply is written from the state, as the simulator's "Where you stand now" lines; before any reading
      // nothing is written at all.
      [Test]
      public void GIVEN_a_state_WHEN_the_note_is_written_THEN_it_holds_the_lines_and_nothing_before_any_reading()
      {
         string note = DramaturgeInnerLife.Note(DramaturgeInnerLife.Apply(new DramaturgeInnerState(), Reading), "Arwa");

         note.Should().Be("- you feel: wary\n- what just changed in you: She saw he lies about the toll, at his second line\n- what you want now: make him pay the toll\n"
                          + "- how you go about it now: press him with the ledger\n- what you let them see: polite patience\n- what you keep hidden: her contempt\n"
                          + "- where you stand with Arwa: Arwa: understands partly, forgives no, loves no, trusts no\n- your pull to speak: high, duty");
         DramaturgeInnerLife.Note(new DramaturgeInnerState(), "Arwa").Should().BeNull();
      }

      // STAKE: the reader must be told the four-part bond and that a lie is never a tactic (the mod's rule: NPCs never lie).
      [Test]
      public void GIVEN_a_sheet_WHEN_the_reader_is_instructed_THEN_it_asks_the_bond_and_forbids_a_lie_as_a_tactic()
      {
         string system = DramaturgeInnerLife.System(new DramaturgeSheet {NpcName = "Alympia", PlayerName = "Arwa", Knows = "A lady of the Empire."});

         system.Should().Contain("A tactic never includes a lie");
         system.Should().Contain("bond: <toward Arwa");
         system.Should().Contain("A lady of the Empire.");
      }
   }
}
