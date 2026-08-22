// Code written by Gabriel Mailhot, 22/08/2026.
//
// ActionMemoryLabel turns an executed bridge action into the short blue tag the Memories panel shows beside a
// journal entry. The tag exists so a memory that carried a REAL mechanical consequence (gold moved, a recruit, a
// fief, a regard shift) reads at a glance, distinct from the LLM's soft narrative classification. What breaks if
// this is wrong: a deed gets no tag (the turning point is invisible), a non-deed flow control earns a spurious
// tag (noise), or a relation shift shows the wrong direction (the player misreads whether they gained or lost
// standing). These tests pin the mapping the panel depends on.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class ActionMemoryLabelTests
   {
      // STAKE: a coin gift is the clearest "something concrete happened" a player scans for; it must read plainly.
      [Test]
      public void Gives_a_plain_label_for_a_coin_deed()
      {
         ActionMemoryLabel.Describe("give_gold").Should().Be("Gift of gold");
      }

      // STAKE: change_relation is the most common deed; the direction is the whole point (did I gain or lose the
      // NPC's regard?), so a positive outcome must read "rose" and a negative "fell", never the same word.
      [Test]
      public void Colours_a_relation_shift_by_direction()
      {
         ActionMemoryLabel.Describe("change_relation", wasPositive: true).Should().Be("Regard rose");
         ActionMemoryLabel.Describe("change_relation", wasPositive: false).Should().Be("Regard fell");
      }

      // STAKE: a recruit / a vassalage / a party join are exactly the turning points the blue tag exists to mark.
      [Test]
      public void Names_the_service_and_prisoner_turning_points()
      {
         ActionMemoryLabel.Describe("join_party").Should().Be("Joined your party");
         ActionMemoryLabel.Describe("join_as_vassal").Should().Be("Sworn as your vassal");
         ActionMemoryLabel.Describe("recruit_prisoner").Should().Be("Recruited from the cells");
         ActionMemoryLabel.Describe("execute_prisoner").Should().Be("Executed");
      }

      // STAKE: chat-flow controls are NOT deeds; tagging them would litter the journal with meaningless blue tags
      // on turns where nothing actually changed, defeating the "real consequence" signal.
      [Test]
      public void Returns_null_for_non_deed_flow_controls()
      {
         ActionMemoryLabel.Describe("end_conversation").Should().BeNull();
         ActionMemoryLabel.Describe("request_privacy").Should().BeNull();
         ActionMemoryLabel.Describe("witness_leaves").Should().BeNull();
         ActionMemoryLabel.Describe("retire").Should().BeNull();
      }

      // STAKE: a blank or missing type must never become a tag (it would render an empty or "  " blue label).
      [Test]
      public void Returns_null_for_a_blank_type()
      {
         ActionMemoryLabel.Describe(null).Should().BeNull();
         ActionMemoryLabel.Describe("   ").Should().BeNull();
      }

      // STAKE: the bridge may handle a verb this map does not spell out yet; rather than show nothing, it must
      // fall back to a readable, capitalised, de-underscored label so a new deed still reads in the journal.
      [Test]
      public void Humanizes_an_unmapped_verb()
      {
         ActionMemoryLabel.Describe("some_new_deed").Should().Be("Some new deed");
      }

      // STAKE: the mapping must not care about the caller's casing or stray spacing (the type comes from parsed
      // LLM/bridge text), or a real deed would slip to the fallback and read oddly.
      [Test]
      public void Is_case_and_whitespace_insensitive()
      {
         ActionMemoryLabel.Describe("  GIVE_GOLD  ").Should().Be("Gift of gold");
      }
   }
}
