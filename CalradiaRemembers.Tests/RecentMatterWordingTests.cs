// Code written by Gabriel Mailhot, 05/10/2026.
// How the matter a character comes to raise is quoted in the "THIS MEETING" block (you sought the player out). fkasad (Nexus,
// 05/10/2026): Dorin sought Bardin out about being freed by him, and from his first line said HE had freed Bardin.
// The block speaks to the character as "you", then pasted his own memory, "You freed me from the hands of Bloody
// Spearz Tribe", in which "you" is the player: nothing marked the switch, and the model read it as "you (Dorin) freed
// me". A lived memory is now quoted as the character's own words, with whose "you" is whose stated beside it. If this
// breaks, a lord comes to thank the player and claims the deed for himself.

#region

using CalradiaRemembers.Logic.Interception;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class RecentMatterWordingTests
   {
      private const string Freed = "You freed me from the hands of Bloody Spearz Tribe. I will not forget that you came for me.";

      // fkasad's case: the memory is quoted, and its "you" is named as the player, its "I" as the character.
      [Test]
      public void GIVEN_a_lived_memory_written_to_the_player_WHEN_the_character_comes_to_raise_it_THEN_whose_you_is_whose_is_said()
      {
         string clause = RecentMatterWording.MatterClause(new RecentMatterWordingFacts {
            Summary = Freed, Register = RecentMatterRegister.LivedMemory, PlayerName = "Bardin Runebound", CharacterName = "Dorin"
         });

         clause.Should().Contain("\"" + Freed + "\"", "the memory is quoted as it was written");
         clause.Should().Contain("\"you\" is Bardin Runebound").And.Contain("\"I\" is you, Dorin");
      }

      // A rumour or gossip is told in the third person already: it is given as it was, with nothing added.
      [Test]
      public void GIVEN_a_rumour_about_the_player_WHEN_raised_THEN_it_is_given_plainly()
      {
         RecentMatterWording.MatterClause(new RecentMatterWordingFacts {
            Summary = "Bardin broke the siege of Rhemtoil.", Register = RecentMatterRegister.RumorAboutPlayer, PlayerName = "Bardin", CharacterName = "Dorin"
         }).Should().Be(" The matter: Bardin broke the siege of Rhemtoil.");
      }

      // With no player name to give, the convention is still stated rather than naming nobody.
      [Test]
      public void GIVEN_no_player_name_WHEN_a_lived_memory_is_raised_THEN_you_is_still_said_to_be_the_player()
      {
         RecentMatterWording.MatterClause(new RecentMatterWordingFacts {Summary = Freed, Register = RecentMatterRegister.LivedMemory, CharacterName = "Dorin"})
                            .Should().Contain("\"you\" is the player");
      }
   }
}
