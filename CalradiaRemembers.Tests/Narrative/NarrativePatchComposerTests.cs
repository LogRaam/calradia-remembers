// Code written by Gabriel Mailhot, 19/09/2026.
// What these pin: that another module can add to the prompt's background prose without destroying anything.
//
// tashmetu (Nexus, 19/09/2026), building a Bellum Civile bridge on the public API: "Right now I have to tell
// users to copy a file into your ModuleData\Custom\, which means overwriting whatever is there and if a second
// mod ever wants to contribute prompt text, it destroys the first one's work."
//
// He proposed the shape and argued it from Bellum's own extension points rather than from taste, and his third
// argument is the one the format has to answer: a small bridge appends a few lines, a total conversion replaces
// the geography outright, and ONE mechanism serves both without the small case being able to cause the large
// one by accident.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Narrative;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Narrative
{
   [TestFixture]
   public sealed class NarrativePatchComposerTests
   {
      private const string Base = "Calradia is a continent at war.\n\nSix cultures compete for it.";

      // THE SMALL CASE, and the common one: a bridge adds what its own mod knows and touches nothing else.
      [Test]
      public void GIVEN_a_patch_that_only_adds_WHEN_composed_THEN_the_base_text_survives_whole()
      {
         string text = Compose(Patch("Bellum", "The Empire is ruled by a privy council."));

         text.Should().Contain("Calradia is a continent at war.");
         text.Should().Contain("Six cultures compete for it.");
         text.Should().Contain("The Empire is ruled by a privy council.");
      }

      // TWO MODULES, AND NEITHER LOSES, which is the whole complaint: today the second one to write wins and
      // the first one's work is gone.
      [Test]
      public void GIVEN_two_modules_contributing_WHEN_composed_THEN_both_contributions_are_present()
      {
         string text = Compose(Patch("Bellum", "A privy council rules."), Patch("Other", "The Nords have returned."));

         text.Should().Contain("A privy council rules.").And.Contain("The Nords have returned.");
      }

      // ORDER IS THE LAUNCHER'S, and it is how precedence is decided: the later module speaks last, so its
      // removals see what the earlier one added.
      [Test]
      public void GIVEN_a_later_module_removing_an_earlier_one_line_WHEN_composed_THEN_the_later_one_wins()
      {
         string text = Compose(Patch("First", "The Nords have returned."),
                               Patch("Second", "!remove Nords"));

         text.Should().NotContain("Nords");
      }

      // THE SUBTRACTIVE HALF, against the mod's own text: a conversion where nobody uses compass names says so
      // by taking the line out, not by asking the player to edit a shipped file.
      [Test]
      public void GIVEN_a_patch_that_removes_a_base_line_WHEN_composed_THEN_that_line_is_gone()
      {
         Compose(Patch("TOR", "!remove Six cultures"))
            .Should().NotContain("Six cultures").And.Contain("Calradia is a continent at war.");
      }

      // THE LARGE CASE: a total conversion's geography is not Calradia's, and it must be able to say so in one
      // line rather than by removing the shipped document sentence by sentence.
      [Test]
      public void GIVEN_a_patch_that_replaces_WHEN_composed_THEN_nothing_of_the_base_remains()
      {
         string text = Compose(Patch("TOR", "!replace\nThe Old World is a place of fading light."));

         text.Should().Be("The Old World is a place of fading light.");
      }

      // AND THE REASON THE MARKER IS "!" RATHER THAN "-". Prose written for a model is full of lines beginning
      // with a dash, because that is how anyone writes a list. If a leading dash meant "delete", a contributor
      // writing an ordinary bullet would silently remove a lord's behaviour, and would have no way to see why.
      [Test]
      public void GIVEN_a_contribution_written_as_a_bulleted_list_WHEN_composed_THEN_nothing_is_deleted()
      {
         string text = Compose(Patch("Bridge", "- Lords speak of the coalition.\n- Merchants speak of grain."));

         text.Should().Contain("Calradia is a continent at war.");
         text.Should().Contain("- Lords speak of the coalition.");
         text.Should().Contain("- Merchants speak of grain.");
      }

      // A REMOVE WITH NOTHING TO REMOVE takes the whole document if it is read literally, and that is never what
      // a typo meant. !replace exists for wanting everything gone.
      [Test]
      public void GIVEN_an_empty_remove_directive_WHEN_composed_THEN_it_is_ignored_rather_than_emptying_everything()
      {
         Compose(Patch("Careless", "!remove   ")).Should().Contain("Calradia is a continent at war.");
      }

      // Removing out of the middle of a document leaves a hole where a paragraph break used to be. Blank lines
      // separate blocks in these files, so a run of them reads as something missing.
      [Test]
      public void GIVEN_a_removal_from_the_middle_WHEN_composed_THEN_no_run_of_blank_lines_is_left_behind()
      {
         Compose(Patch("TOR", "!remove Six cultures")).Should().NotContain("\n\n\n");
      }

      // NO PATCHES MEANS NO CHANGE. Every player without a bridge mod installed must get byte-for-byte what the
      // mod shipped, or this feature has a cost for everyone and a benefit for a few.
      [Test]
      public void GIVEN_no_patches_at_all_WHEN_composed_THEN_the_text_is_what_the_mod_shipped()
      {
         NarrativePatchComposer.Compose(Base, null).Should().Be(Base, "byte for byte, line endings included");
         NarrativePatchComposer.Compose(Base, new List<NarrativePatch>()).Should().Be(Base);
         NarrativePatchComposer.Compose(Base, new[] {new NarrativePatch("Shipped an empty file", "")}).Should().Be(Base);
      }

      // An empty file a module shipped by accident must do nothing rather than blank the document.
      [Test]
      public void GIVEN_a_patch_file_with_nothing_in_it_WHEN_composed_THEN_nothing_changes()
      {
         Compose(Patch("Empty", "   ")).Should().Contain("Six cultures compete for it.");
      }

      #region what tashmetu's first real contribution taught (19/09/2026)

      private const string Guidelines = @"=== MEMORY AND CONSEQUENCES ===

Severe betrayal changes you.

=== FACTION BEHAVIOR ===

If you are from the Southern Empire:
You are shaped by Empress Rhagaea's progressive vision.

If you are from the Nords:
You are a seafarer of raider stock.";

      // THE CASE THAT WOULD HAVE SHIPPED BROKEN. The shipped guidelines end with the per-culture blocks, Nords
      // last, and each character keeps only their own culture's block. A contribution appended straight after
      // it became part of the Nords block, so every lord who was not a Nord lost it without a word. This runs
      // the two real policies end to end, compose then narrow, the way the game does.
      [Test]
      public void GIVEN_a_contribution_to_the_guidelines_WHEN_an_imperial_lord_reads_them_THEN_it_is_still_there()
      {
         string composed = NarrativePatchComposer.Compose(Guidelines,
            new[] {new NarrativePatch("BellumBridge", "A seat on the privy council is not a feudal title.")});

         string imperial = FactionGuidelinesPolicy.NarrowToOwnCulture(composed, "Empire", "Southern Empire");

         imperial.Should().Contain("A seat on the privy council is not a feudal title.");
         imperial.Should().NotContain("seafarer of raider stock", "the narrowing itself must still work");
      }

      // The boundary is only drawn when there is something above to separate from; a contributor who opened with
      // a heading of their own has drawn it already, and a second would be an empty section.
      [Test]
      public void GIVEN_a_contribution_that_opens_with_its_own_heading_WHEN_composed_THEN_no_second_heading_is_added()
      {
         string composed = NarrativePatchComposer.Compose(Guidelines,
            new[] {new NarrativePatch("BellumBridge", "=== BELLUM CIVILE ===\nOffices are held at the crown's pleasure.")});

         composed.Should().Contain("=== BELLUM CIVILE ===");
         composed.Should().NotContain(NarrativePatchComposer.ContributionHeading);
      }

      // THE COMMENT. His files opened with a paragraph explaining cr_patch to a human, and every word of it went to
      // the model on every call. A contributor must be able to document the file without the lords hearing it.
      [Test]
      public void GIVEN_comment_lines_in_a_contribution_WHEN_composed_THEN_the_model_never_reads_them()
      {
         string composed = Compose(Patch("Bridge",
            "!# Bellum Bridge: appended after the mod's own text and your Custom folder.\n" +
            "Offices are held at the crown's pleasure."));

         composed.Should().NotContain("Custom folder");
         composed.Should().Contain("Offices are held at the crown's pleasure.");
      }

      // A file of nothing but notes changes nothing, byte for byte: the same promise as a module with no file.
      [Test]
      public void GIVEN_a_contribution_that_is_only_comments_WHEN_composed_THEN_the_shipped_text_is_returned_unchanged()
      {
         Compose(Patch("Bridge", "!# nothing to add yet\n\n!# still nothing")).Should().Be(Base);
      }

      #endregion

      #region private

      private static NarrativePatch Patch(string source, string text) => new(source, text);

      private static string Compose(params NarrativePatch[] patches)
         => NarrativePatchComposer.Compose(Base, patches);

      #endregion
   
      // tashmetu (Nexus, 27/09/2026): to change ONE culture block he had to !remove the whole section line by line and
      // write it back, 50 lines across five files, and any line CR later rewords would silently stay behind as a stray
      // sentence. "!block" replaces or adds one "If you are from ..." block by its HEADING, whatever its lines say.
      private const string WithCultures =
         "=== FACTION BEHAVIOR ===\n\nIf you are from Vlandia:\nYou believe in knightly honor.\nYou prize the tourney.\n\n"
       + "If you are from Battania:\nYou come from a proud clan culture.\n\n=== CLOSING ===\nStay in character.";

      private static string ComposeOver(string baseText, params NarrativePatch[] patches)
         => NarrativePatchComposer.Compose(baseText, patches);

      // The block is replaced by its heading: every line of the old one goes, however it was worded, and nothing else moves.
      [Test]
      public void GIVEN_a_block_directive_for_an_existing_heading_WHEN_composed_THEN_that_block_alone_is_replaced()
      {
         string text = ComposeOver(WithCultures, Patch("Rhodoks", "!block If you are from Vlandia:\nYou serve a lord of the mountain roads."));

         text.Should().Contain("If you are from Vlandia:\nYou serve a lord of the mountain roads.");
         text.Should().NotContain("knightly honor").And.NotContain("tourney");
         text.Should().Contain("You come from a proud clan culture.").And.Contain("=== CLOSING ===");
      }

      // A NEW people goes INSIDE the faction section, where each character is shown only their own block. Appended at
      // the end under a contribution heading, it would be read by everyone as general guidance.
      [Test]
      public void GIVEN_a_block_directive_for_a_new_heading_WHEN_composed_THEN_it_joins_the_faction_section()
      {
         string text = ComposeOver(WithCultures, Patch("Rhodoks", "!block the Rhodoks\nYou are a pikeman of the mountain cantons."));

         int block = text.IndexOf("If you are from the Rhodoks:", System.StringComparison.Ordinal);
         block.Should().BeGreaterThan(text.IndexOf("=== FACTION BEHAVIOR ===", System.StringComparison.Ordinal));
         block.Should().BeLessThan(text.IndexOf("=== CLOSING ===", System.StringComparison.Ordinal));
         text.Should().Contain("You are a pikeman of the mountain cantons.");
         text.Should().NotContain(NarrativePatchComposer.ContributionHeading);
      }

      // A block directive with nothing under it takes that people's block out.
      [Test]
      public void GIVEN_an_empty_block_directive_WHEN_composed_THEN_that_block_is_removed()
      {
         string text = ComposeOver(WithCultures, Patch("TOR", "!block Battania"));

         text.Should().NotContain("If you are from Battania:").And.NotContain("proud clan culture");
         text.Should().Contain("knightly honor");
      }

      // Several blocks in one patch: each ends where the next directive begins.
      [Test]
      public void GIVEN_two_block_directives_in_one_patch_WHEN_composed_THEN_each_gets_its_own_lines()
      {
         string text = ComposeOver(WithCultures,
            Patch("Rhodoks", "!block Vlandia\nKnights of the west.\n!block Battania\nMen of the forest."));

         text.Should().Contain("If you are from Vlandia:\nKnights of the west.");
         text.Should().Contain("If you are from Battania:\nMen of the forest.");
      }
}
}
