// Code written by Gabriel Mailhot, 20/10/2026.
// What the chat's Memories panel shows of a character's private memories. tashmetu (20/10/2026): a player read about their
// character's wife's secret affair in her lover's memories before anything had come to light, because the panel listed the
// private memory Relations & Schemes wrote. He asked for them left out, or marked as secret, with a setting for players who
// like to see everything; Gabriel approved. If this is wrong, the panel spoils another mod's secrets, or hides from the
// player a word the player himself gave a companion in private.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class MemoryPanelPolicyTests
   {
      // STAKE: tashmetu's report. The affair another mod keeps secret must not be read in the panel by default.
      [Test]
      public void GIVEN_a_secret_another_mod_wrote_WHEN_the_player_has_not_asked_to_see_secrets_THEN_the_panel_leaves_it_out()
      {
         MemoryPanelPolicy.Show(new MemoryPanelFacts {IsPrivate = true, FromAnotherMod = true}).Should().Be(MemoryPanelShow.Hidden);
      }

      // STAKE: "A setting would do as well, for players who like to see everything": they see it, and see it is a secret.
      [Test]
      public void GIVEN_a_secret_another_mod_wrote_WHEN_the_player_asked_to_see_secrets_THEN_it_is_listed_marked_secret()
      {
         MemoryPanelPolicy.Show(new MemoryPanelFacts {IsPrivate = true, FromAnotherMod = true, ShowSecrets = true})
            .Should().Be(MemoryPanelShow.MarkedSecret);
      }

      // STAKE: CR's own private memories are the player's word to a companion before a meeting (the briefing). The player
      // gave it; hiding it would hide from the player something he did himself.
      [Test]
      public void GIVEN_a_private_word_the_player_gave_a_companion_WHEN_listing_THEN_it_stays_in_the_panel()
      {
         MemoryPanelPolicy.Show(new MemoryPanelFacts {IsPrivate = true, FromAnotherMod = false}).Should().Be(MemoryPanelShow.Plain);
      }

      // STAKE: only secrets are held back. A memory another mod writes openly (a tale's ending, a rumour) is listed as always.
      [Test]
      public void GIVEN_an_open_memory_another_mod_wrote_WHEN_listing_THEN_it_is_listed_as_always()
      {
         MemoryPanelPolicy.Show(new MemoryPanelFacts {IsPrivate = false, FromAnotherMod = true}).Should().Be(MemoryPanelShow.Plain);
      }

      // STAKE: CR's tales write through the same public door under its own name; that is still CR, and a memory with no
      // source at all was written by CR (or stored before sources were kept).
      [TestCase("CalradiaRemembers", false)]
      [TestCase("", false)]
      [TestCase(null, false)]
      [TestCase("RelationsAndSchemes", true)]
      public void GIVEN_a_memory_source_WHEN_asking_if_another_mod_wrote_it_THEN_only_another_name_counts(string source, bool expected)
      {
         MemoryPanelPolicy.IsAnotherMod(source).Should().Be(expected);
      }
   }
}
