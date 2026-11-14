// Code written by Gabriel Mailhot, 14/11/2026.
// What CharacterTextComposer decides: which pieces other mods write of a character (CrCharacterText) reach the prompt and
// the panel. Bhelogan (Discord, 14/11/2026): "Appearance: shown in the panel, given to the model; Private: model only,
// never in the panel; a budget around 1,200 characters ... stable within a conversation, so it can sit in the cached
// prefix." If this breaks, a secret shows in the panel, a description is cut or lost, the Compact prompt overflows for small
// models, or the prompt reorders from turn to turn and loses its cache.

#region

using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CharacterTextComposerTests
   {
      private static CharacterTextPiece Piece(string source, CharacterTextSection section, string text, bool panel = true, bool lean = false)
         => new() {Source = source, Section = section, Text = text, ShowInPanel = panel, Lean = lean};

      // THE REPORT: a 1,200-character description reaches the model whole (200 was too short).
      [Test]
      public void GIVEN_a_description_of_1200_characters_WHEN_composed_for_the_prompt_THEN_it_is_kept_whole()
      {
         string description = new string('a', CharacterTextComposer.MaxChars);

         CharacterTextComposer.ForPrompt(new[] {Piece("Notebook", CharacterTextSection.Appearance, description)}, CharacterTextSection.Appearance, false)
                              .Should().Be(description);
      }

      // STAKE: a piece over the budget is left out whole rather than cut mid-sentence.
      [Test]
      public void GIVEN_a_piece_over_the_budget_WHEN_composed_THEN_it_is_left_out()
      {
         string tooLong = new string('a', CharacterTextComposer.MaxChars + 1);

         CharacterTextComposer.ForPrompt(new[] {Piece("Notebook", CharacterTextSection.Appearance, tooLong)}, CharacterTextSection.Appearance, false)
                              .Should().BeNull();
      }

      // THE REPORT: "Private: model only, never in the panel", whatever the mod asked.
      [Test]
      public void GIVEN_a_private_piece_asked_for_the_panel_WHEN_composed_THEN_the_model_gets_it_and_the_panel_never()
      {
         CharacterTextPiece secret = Piece("Secrets", CharacterTextSection.Private, "He poisoned his brother.", panel: true);

         CharacterTextComposer.ForPrompt(new[] {secret}, CharacterTextSection.Private, false).Should().Be("He poisoned his brother.");
         CharacterTextComposer.ForPanel(new[] {secret}, CharacterTextSection.Private).Should().BeNull();
      }

      // STAKE: a piece the mod keeps out of the panel still reaches the model.
      [Test]
      public void GIVEN_a_piece_not_for_the_panel_WHEN_composed_THEN_only_the_model_gets_it()
      {
         CharacterTextPiece piece = Piece("Notebook", CharacterTextSection.Backstory, "He was a monk once.", panel: false);

         CharacterTextComposer.ForPrompt(new[] {piece}, CharacterTextSection.Backstory, false).Should().Be("He was a monk once.");
         CharacterTextComposer.ForPanel(new[] {piece}, CharacterTextSection.Backstory).Should().BeNull();
      }

      // STAKE: the Compact prompt for small models carries only short pieces the mod marked for it.
      [Test]
      public void GIVEN_the_compact_prompt_WHEN_composed_THEN_only_short_pieces_marked_lean_are_kept()
      {
         var pieces = new[] {
            Piece("A", CharacterTextSection.Appearance, "Short and marked.", lean: true),
            Piece("B", CharacterTextSection.Appearance, "Short, not marked."),
            Piece("C", CharacterTextSection.Appearance, new string('a', CharacterTextComposer.MaxLeanChars + 1), lean: true)
         };

         CharacterTextComposer.ForPrompt(pieces, CharacterTextSection.Appearance, true).Should().Be("Short and marked.");
      }

      // STAKE: the order follows the mods' names, not the order they registered, so the cached prompt stays the same.
      [Test]
      public void GIVEN_pieces_from_several_mods_WHEN_composed_THEN_they_are_ordered_by_source()
      {
         var pieces = new[] {Piece("Zeta", CharacterTextSection.Backstory, "Z."), Piece("Alpha", CharacterTextSection.Backstory, "A.")};

         CharacterTextComposer.ForPrompt(pieces, CharacterTextSection.Backstory, false).Should().Be("A.\nZ.");
      }

      // STAKE: many mods together cannot flood one section of the prompt.
      [Test]
      public void GIVEN_more_pieces_than_allowed_WHEN_composed_THEN_only_the_first_three_are_kept()
      {
         var pieces = new[] {
            Piece("A", CharacterTextSection.Backstory, "1."), Piece("B", CharacterTextSection.Backstory, "2."),
            Piece("C", CharacterTextSection.Backstory, "3."), Piece("D", CharacterTextSection.Backstory, "4.")
         };

         CharacterTextComposer.ForPrompt(pieces, CharacterTextSection.Backstory, false).Should().Be("1.\n2.\n3.");
      }

      // STAKE: CR's own appearance comes first and is never replaced by a mod's.
      [Test]
      public void GIVEN_crs_appearance_and_a_mods_WHEN_appended_THEN_crs_comes_first()
      {
         CharacterTextComposer.Append("Tall, with dark hair.", "A scar runs over his brow.").Should().Be("Tall, with dark hair. A scar runs over his brow.");
         CharacterTextComposer.Append(null, "A scar.").Should().Be("A scar.");
         CharacterTextComposer.Append("  ", null).Should().BeNull();
      }
   }
}
