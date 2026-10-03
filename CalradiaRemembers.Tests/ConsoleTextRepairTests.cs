// Code written by Gabriel Mailhot, 03/10/2026.
// Shinelegacy (Nexus, 03/10/2026, on 2.6.10): "toute creation ou modification via cr.memory_add ou cr.memory_set
// transforme les caracteres Unicode : e-acute -> Ã©, e-grave -> Ã¨, c-cedilla -> Ã§, oe -> Å“". The game's console hands
// a command its arguments as UTF-8 bytes read back as Windows-1252, before any CR code runs; memories CR writes itself
// were never touched. CR repairs the text of its own commands on the way in. If this rule is wrong, a French (or any
// accented) memory typed at the console is stored as mojibake, or a text that was fine is changed.

#region

using CalradiaRemembers.Logic.Diagnostics;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ConsoleTextRepairTests
   {
      // Shinelegacy's own test line, as the console delivered it, comes back exactly as typed.
      [Test]
      public void GIVEN_accented_text_mangled_by_the_console_WHEN_repaired_THEN_it_reads_exactly_as_typed()
      {
         const string typed = "TEST Unicode : é è ê à ù ç œ É È Ê À Ù Ç Œ";

         ConsoleTextRepair.Repair(Mangle(typed)).Should().Be(typed);
      }

      // Text that arrived intact (plain English, or accents a console passed through correctly) is never changed.
      [TestCase("We fought side by side at Pendraic.")]
      [TestCase("Elle était là, à Liena.")]
      [TestCase("")]
      [TestCase(null)]
      public void GIVEN_text_that_arrived_intact_WHEN_repaired_THEN_it_is_left_as_it_is(string text)
      {
         ConsoleTextRepair.Repair(text).Should().Be(text);
      }

      // The repair is safe to run twice: a second pass finds nothing to undo.
      [Test]
      public void GIVEN_text_already_repaired_WHEN_repaired_again_THEN_it_does_not_change()
      {
         string once = ConsoleTextRepair.Repair(Mangle("Liéna, cœur de lion"));

         ConsoleTextRepair.Repair(once).Should().Be(once);
      }

      // The reproduction is the player's own observation, character for character, so the repair is tested against
      // what the console really delivers and not against a guess.
      [Test]
      public void GIVEN_the_console_mangling_reproduced_WHEN_compared_to_the_report_THEN_it_matches_what_the_player_saw()
      {
         Mangle("é è ê ç œ").Should().Be("Ã© Ã¨ Ãª Ã§ Å“");
      }

      private static string Mangle(string s) => ConsoleTextRepair.Mangle(s);
   }
}
