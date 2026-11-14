// Code written by Gabriel Mailhot, 14/11/2026.
// What PromptPlayerIdentity decides: when the player the character prompt was built for is no longer the player.
// Icezera (Discord, 13/11/2026): after a player switch, "NPC's are still thinking that I'm the original character". If this
// breaks, characters keep calling the player by the name of the hero they no longer play, until the save is reloaded.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PromptPlayerIdentityTests
   {
      // THE REPORT: a switch to another hero of the same clan and realm kept the old name in every prompt.
      [Test]
      public void GIVEN_a_switch_to_another_hero_of_the_same_clan_WHEN_the_stamps_are_compared_THEN_they_differ()
      {
         string before = PromptPlayerIdentity.Stamp("main_hero", "Dinkle", false, "Trollisus", "Empire");
         string after = PromptPlayerIdentity.Stamp("lord_sister", "Ira", true, "Trollisus", "Empire");

         after.Should().NotBe(before);
      }

      // STAKE: the same player, unchanged, keeps the prompt already built (no rebuild on every reply).
      [Test]
      public void GIVEN_the_same_player_WHEN_the_stamps_are_compared_THEN_they_are_equal()
      {
         PromptPlayerIdentity.Stamp("main_hero", "Dinkle", false, "Trollisus", "Empire")
                             .Should().Be(PromptPlayerIdentity.Stamp("main_hero", "Dinkle", false, "Trollisus", "Empire"));
      }

      // STAKE: a hero renamed in the game is called by the new name.
      [Test]
      public void GIVEN_the_same_hero_renamed_WHEN_the_stamps_are_compared_THEN_they_differ()
      {
         PromptPlayerIdentity.Stamp("main_hero", "Dinkle", false, "Trollisus", "Empire")
                             .Should().NotBe(PromptPlayerIdentity.Stamp("main_hero", "Dinkle the Bold", false, "Trollisus", "Empire"));
      }

      // STAKE: joining another clan or realm still rebuilds, as it did before the switch fix.
      [Test]
      public void GIVEN_the_same_hero_in_another_realm_WHEN_the_stamps_are_compared_THEN_they_differ()
      {
         PromptPlayerIdentity.Stamp("main_hero", "Dinkle", false, "Trollisus", "Empire")
                             .Should().NotBe(PromptPlayerIdentity.Stamp("main_hero", "Dinkle", false, "Trollisus", "Vlandia"));
      }
   }
}
