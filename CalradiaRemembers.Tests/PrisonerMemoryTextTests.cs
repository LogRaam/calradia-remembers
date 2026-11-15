// Code written by Gabriel Mailhot, 23/10/2026.
// PrisonerMemoryText words what a captive and a dungeon's lord keep of the player's prisoners. chmidt (Nexus, 23/10/2026):
// lords "should also know who I put in their dungeons". If this is wrong, a lord the player took remembers nothing of it,
// the lord whose dungeon holds a prisoner cannot say who left him there, or a captivity's end is told wrong.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PrisonerMemoryTextTests
   {
      // STAKE: a lord the player took remembers being taken, and where.
      [Test]
      public void GIVEN_a_lord_taken_after_a_battle_WHEN_he_remembers_THEN_he_names_the_player_and_the_place()
      {
         PrisonerMemoryText.Taken("Arwa", true, "Pravend").Should().Be("Taken prisoner by Arwa after the battle near Pravend.");
         PrisonerMemoryText.Taken("Arwa", true, null).Should().Be("Taken prisoner by Arwa after a battle.");
         PrisonerMemoryText.Taken("Arwa", false, "Pravend").Should().Be("Taken prisoner by Arwa near Pravend.");
      }

      // STAKE: chmidt: the lord whose dungeon holds the prisoner knows who left him there.
      [Test]
      public void GIVEN_a_prisoner_left_in_a_lords_dungeon_WHEN_both_remember_THEN_each_tells_his_side()
      {
         PrisonerMemoryText.LeftInDungeon("Arwa", "Hargard").Should().Be("Arwa left me in the dungeon of Hargard.");
         PrisonerMemoryText.DepositedWithMe(new PrisonerDepositNames {PlayerName = "Arwa", Captive = "Lucon of the dey Meroc", Place = "Hargard"}).Should().Be("Arwa left Lucon of the dey Meroc in my dungeon at Hargard.");
      }

      // STAKE: how a captivity the player held ended, each in its own words.
      [TestCase(CaptivityEnd.Ransomed, "Ransomed out of Arwa's captivity.")]
      [TestCase(CaptivityEnd.PeaceMade, "Went home from Arwa's captivity when peace was made.")]
      [TestCase(CaptivityEnd.FreedInBattle, "Freed from Arwa's captivity when my own side won the field.")]
      [TestCase(CaptivityEnd.Escaped, "Escaped from Arwa's captivity.")]
      [TestCase(CaptivityEnd.SetFree, "Set free by Arwa.")]
      [TestCase(CaptivityEnd.Compensated, "Released from Arwa's captivity for a price paid.")]
      public void GIVEN_a_captivity_the_player_held_WHEN_it_ends_THEN_the_captive_says_how(CaptivityEnd how, string expected)
      {
         PrisonerMemoryText.Released(how, "Arwa", null).Should().Be(expected);
      }

      // STAKE: a prisoner the player left in another lord's dungeon was not in the player's own hands when it ended.
      [Test]
      public void GIVEN_a_prisoner_left_in_a_dungeon_WHEN_he_escapes_THEN_the_dungeon_is_named_with_who_left_him()
      {
         PrisonerMemoryText.Released(CaptivityEnd.Escaped, "Arwa", "Hargard").Should().Be("Escaped from the dungeon of Hargard, where Arwa had left me.");
      }
   }
}
