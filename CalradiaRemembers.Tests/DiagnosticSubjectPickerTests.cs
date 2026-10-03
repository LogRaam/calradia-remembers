// Code written by Gabriel Mailhot, 02/10/2026.
// tashmetu (Nexus, 02/10/2026, testing Bridges to Remember on 2.6.10): "cr.prompt_dump <name> picks the first match
// when names repeat. Could it also accept a hero id?" A diagnostic that silently dumps the WRONG namesake's prompt is
// worse than none: the modder reads a stranger's prompt and draws conclusions about the character they meant. The
// picker now takes an exact hero id first, prefers an exact name over a fragment, and says who else answered to the
// name, with their ids, so the next call can be exact.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DiagnosticSubjectPickerTests
   {
      private static readonly List<DiagnosticSubject> Heroes = new()
      {
         new DiagnosticSubject {Id = "lord_1_1", Name = "Erevan"},
         new DiagnosticSubject {Id = "tbr_sister_7", Name = "Erevan"},
         new DiagnosticSubject {Id = "lord_2_3", Name = "Erevanna"}
      };

      // tashmetu's ask: a hero id names exactly one person, whoever shares her name.
      [Test]
      public void GIVEN_a_hero_id_WHEN_picking_THEN_that_exact_hero_is_dumped()
      {
         DiagnosticPick pick = DiagnosticSubjectPicker.Pick(Heroes, "tbr_sister_7");

         pick.Chosen.Id.Should().Be("tbr_sister_7");
         pick.Others.Should().BeEmpty();
      }

      // Two people answer to the name: the modder is told who else did, with ids, instead of never learning there was a choice.
      [Test]
      public void GIVEN_namesakes_WHEN_picking_by_name_THEN_the_others_are_named_with_their_ids()
      {
         DiagnosticPick pick = DiagnosticSubjectPicker.Pick(Heroes, "Erevan");

         pick.Chosen.Id.Should().Be("lord_1_1");
         pick.Others.Should().ContainSingle(o => o.Id == "tbr_sister_7");
      }

      // "Erevan" must not lose to "Erevanna" or list her as a namesake: an exact name outranks a fragment of a longer one.
      [Test]
      public void GIVEN_an_exact_name_and_a_longer_name_containing_it_WHEN_picking_THEN_the_exact_name_wins_alone()
      {
         DiagnosticSubjectPicker.Pick(Heroes, "erevan").Others.Should().NotContain(o => o.Id == "lord_2_3");
      }

      // A fragment still works as it always did, for the player who types half a name.
      [Test]
      public void GIVEN_only_a_fragment_WHEN_picking_THEN_the_hero_whose_name_contains_it_is_chosen()
      {
         DiagnosticSubjectPicker.Pick(Heroes, "vanna").Chosen.Id.Should().Be("lord_2_3");
      }

      // Nobody answers: no one is chosen, so the command says so rather than dumping somebody.
      [Test]
      public void GIVEN_no_match_WHEN_picking_THEN_nobody_is_chosen()
      {
         DiagnosticSubjectPicker.Pick(Heroes, "Rhagaea").Chosen.Should().BeNull();
      }
   }
}
