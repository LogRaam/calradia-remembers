// Code written by Gabriel Mailhot, 14/11/2026.
// What PanelTextCleaner decides: what of an authored text the conversation panel shows a human. Bhelogan (Discord,
// 14/11/2026) showed the Character section reading "[DNB_PHYSICAL_DESCRIPTION] Physical description: ...
// [/DNB_PHYSICAL_DESCRIPTION] [HSAI_SECRET:hs_secret_...]". If this breaks, the player reads other mods' markers again, or
// a character's secret before discovering it; if it widens, words the player wrote between brackets vanish from the panel.

#region

using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PanelTextCleanerTests
   {
      // THE REPORT: the description between another mod's markers reads as plain prose.
      [Test]
      public void GIVEN_a_description_between_a_mods_markers_WHEN_shown_in_the_panel_THEN_the_markers_go_and_the_words_stay()
      {
         string text = "[DNB_PHYSICAL_DESCRIPTION]\nPhysical description: Semnon is a broad-shouldered man.\n[/DNB_PHYSICAL_DESCRIPTION]";

         PanelTextCleaner.ForPanel(text).Should().Be("Physical description: Semnon is a broad-shouldered man.");
      }

      // THE REPORT: the reference token another mod leaves for a secret is not shown.
      [Test]
      public void GIVEN_a_reference_token_WHEN_shown_in_the_panel_THEN_it_is_left_out()
      {
         PanelTextCleaner.ForPanel("A kind surgeon. [HSAI_SECRET:hs_secret_e5slxqKzuYtv_d26ee0f8]").Should().Be("A kind surgeon.");
      }

      // STAKE: a secret written as a block is never read in the panel before the player discovers it.
      [Test]
      public void GIVEN_a_secret_block_WHEN_shown_in_the_panel_THEN_it_is_left_out_whole()
      {
         string text = "A kind surgeon.\n[HSAI_SECRET]He poisoned his brother.[/HSAI_SECRET]\nHe loves dogs.";

         PanelTextCleaner.ForPanel(text).Should().Be("A kind surgeon.\n\nHe loves dogs.");
      }

      // STAKE: a secret block left unclosed still hides what follows rather than spilling it.
      [Test]
      public void GIVEN_an_unclosed_private_block_WHEN_shown_in_the_panel_THEN_everything_after_it_is_left_out()
      {
         PanelTextCleaner.ForPanel("A kind surgeon. [MY_PRIVATE_NOTES] He poisoned his brother.").Should().Be("A kind surgeon.");
      }

      // STAKE: the player's own words in brackets stay as written.
      [Test]
      public void GIVEN_ordinary_brackets_WHEN_shown_in_the_panel_THEN_they_stay()
      {
         PanelTextCleaner.ForPanel("He spells it 'surgeon' [sic] and [laughs] at it.").Should().Be("He spells it 'surgeon' [sic] and [laughs] at it.");
      }

      // STAKE: a backstory without markers is shown unchanged.
      [Test]
      public void GIVEN_a_plain_backstory_WHEN_shown_in_the_panel_THEN_it_is_unchanged()
      {
         PanelTextCleaner.ForPanel("A hard-bitten veteran of the border marches.").Should().Be("A hard-bitten veteran of the border marches.");
      }
   }
}
