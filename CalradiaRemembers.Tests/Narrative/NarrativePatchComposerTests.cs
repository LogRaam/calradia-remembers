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

      #region private

      private static NarrativePatch Patch(string source, string text) => new(source, text);

      private static string Compose(params NarrativePatch[] patches)
         => NarrativePatchComposer.Compose(Base, patches);

      #endregion
   }
}
