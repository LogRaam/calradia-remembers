// Code written by Gabriel Mailhot, 15/11/2026.
// FoldableSectionHeader writes the header of an encyclopedia section the player can fold (Friends and enemies, Houses).
// LapitaPolata (Discord, 15/11/2026): "The longer I play the game, the longer the lists have become." If this is wrong, a
// folded section reads as empty, the header loses its own words, or the switch shows the opposite of what a click does.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class FoldableSectionHeaderTests
   {
      // STAKE (LapitaPolata): a folded section must still say there is something under it, and how much.
      [Test]
      public void GIVEN_a_folded_section_WHEN_its_header_is_written_THEN_it_keeps_its_words_and_counts_the_hidden_rows()
      {
         FoldableSectionHeader.Text("Friends and enemies, and why:", 3, false).Should().Be("Friends and enemies, and why (3) [+]");
         FoldableSectionHeader.Text("Houses (Fen Gwyn, through Caladog):", 12, false).Should().Be("Houses (Fen Gwyn, through Caladog) (12) [+]");
      }

      // STAKE: the mark says what a click does: [-] folds a shown section, [+] opens a folded one.
      [Test]
      public void GIVEN_a_shown_section_WHEN_its_header_is_written_THEN_it_offers_to_fold()
      {
         FoldableSectionHeader.Text("Why:", 2, true).Should().Be("Why (2) [-]");
      }

      // STAKE: a section with nothing in it keeps no header at all (the page hid it before the switch existed).
      [TestCase(null, 3)]
      [TestCase(" ", 3)]
      [TestCase("Houses:", 0)]
      public void GIVEN_no_label_or_no_row_WHEN_the_header_is_written_THEN_it_is_empty(string label, int rows)
      {
         FoldableSectionHeader.Text(label, rows, true).Should().BeEmpty();
      }
   }
}
