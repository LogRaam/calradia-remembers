// Code written by Gabriel Mailhot, 08/10/2026.
// Where a staged conversation in a settlement opens. Gabriel's play test (08/10/2026): after a town duel with Tharos the
// Carpenter, the post-duel conversation opened in the lord's hall, a place Tharos never stands, so the game's
// conversation with him never began and the chat opened over an empty hall, without him. Every staged scene opened the
// lord's hall (or the tavern for a companion), right for a lord and wrong for a notable in the square or a gang leader in
// his tavern. The scene now opens where the character actually is, when the game knows it.
// If this breaks, the player talks to a character who is not in the room.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class AudienceLocationPolicyTests
   {
      private static readonly string[] Town = {"lordshall", "center", "tavern", "prison"};

      // Gabriel's Tharos: a notable stands in the square; the scene opens there, not in the hall.
      [Test]
      public void GIVEN_a_character_the_game_places_in_the_square_WHEN_a_scene_is_staged_THEN_it_opens_in_the_square()
      {
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = "center", Available = Town}).Should().Be("center");
      }

      // A lord in his hall, a gang leader in his tavern: wherever the game has them.
      [Test]
      public void GIVEN_a_character_in_the_hall_or_the_tavern_WHEN_staged_THEN_it_opens_where_they_are()
      {
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = "tavern", Available = Town}).Should().Be("tavern");
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = "lordshall", Available = Town, PreferTavern = true})
                               .Should().Be("lordshall");
      }

      // Nowhere the game knows (a lord just arrived, a character spawned for a duel): the old choice, unchanged.
      [Test]
      public void GIVEN_a_character_the_game_has_placed_nowhere_WHEN_staged_THEN_the_former_choice_holds()
      {
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = null, Available = Town}).Should().Be("lordshall");
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = null, Available = Town, PreferTavern = true}).Should().Be("tavern");
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = null, Available = new[] {"village_center"}}).Should().Be("village_center");
      }

      // A cell is no place for a conversation the player did not ask to hold there: a prisoner is met as before.
      [Test]
      public void GIVEN_a_character_in_the_cells_WHEN_staged_THEN_the_former_choice_holds()
      {
         AudienceLocationPolicy.Choose(new AudienceLocationFacts {WhereTheCharacterIs = "prison", Available = Town}).Should().Be("lordshall");
      }
   }
}
