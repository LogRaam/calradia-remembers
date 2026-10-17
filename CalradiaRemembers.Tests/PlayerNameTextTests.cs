// Code written by Gabriel Mailhot, 17/10/2026.
// What the screen shows where a stored or written text says "the player" (Gabriel, 17/10/2026: never "the player" in
// the game, the character by name). Wrong one way, a grievance or a memory breaks the fourth wall; wrong the other,
// a word that only looks like it ("players", "the player's" possessive) comes out mangled.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PlayerNameTextTests
    {
        // STAKE: the reported grievance; the possessive keeps its "'s" after the name.
        [Test]
        public void GIVEN_a_stored_cause_naming_the_player_WHEN_it_is_shown_THEN_it_names_the_character()
        {
            PlayerNameText.ForDisplay("The marriage was ended by the player's own choice.", "Arwen")
                          .Should().Be("The marriage was ended by Arwen's own choice.");
        }

        // STAKE: a sentence that opens on the role word, or says "the player character", reads with the name too.
        [Test]
        public void GIVEN_the_role_word_capitalised_or_as_player_character_WHEN_it_is_shown_THEN_both_read_the_name()
        {
            PlayerNameText.ForDisplay("The player chose to end it.", "Arwen").Should().Be("Arwen chose to end it.");
            PlayerNameText.ForDisplay("She trusts the player character.", "Arwen").Should().Be("She trusts Arwen.");
        }

        // STAKE: "players" is not the player; a text about players in general must not be rewritten.
        [Test]
        public void GIVEN_a_plural_or_no_role_word_WHEN_it_is_shown_THEN_it_is_unchanged()
        {
            PlayerNameText.ForDisplay("All the players' banners.", "Arwen").Should().Be("All the players' banners.");
            PlayerNameText.ForDisplay("Spoke with Arwen.", "Arwen").Should().Be("Spoke with Arwen.");
        }

        // STAKE: with no campaign loaded (no name), the text is shown as it is rather than with a blank hole.
        [Test]
        public void GIVEN_no_name_known_WHEN_a_text_is_shown_THEN_it_is_unchanged()
        {
            PlayerNameText.ForDisplay("Set aside by the player.", null).Should().Be("Set aside by the player.");
            PlayerNameText.ForDisplay("Set aside by the player.", " ").Should().Be("Set aside by the player.");
            PlayerNameText.ForDisplay(null, "Arwen").Should().BeNull();
        }
    }
}
