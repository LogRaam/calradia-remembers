// Code written by Gabriel Mailhot, 17/10/2026.
// ReplyLanguagePolicy decides the language characters speak when the player named none, and how an opening line is
// told it. RaveN017Mod (Nexus, 17/10/2026), French game, French setting: a character who came to him on the map, and
// every council, opened in English until asked to repeat in French; he asked whether the mod could read the game's
// language. If Resolve is wrong, a French game stays English or an English player is forced into another tongue; if
// WithLanguage is wrong, openings stay English.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ReplyLanguagePolicyTests
   {
      // STAKE: RaveN017Mod's question: a French game and no setting speaks French.
      [Test]
      public void GIVEN_a_french_game_and_no_setting_WHEN_the_language_is_resolved_THEN_it_is_french()
      {
         ReplyLanguagePolicy.Resolve("", "Français").Should().Be("French");
      }

      // STAKE: the player's own setting always wins over the game's language.
      [Test]
      public void GIVEN_a_setting_WHEN_the_language_is_resolved_THEN_the_setting_wins()
      {
         ReplyLanguagePolicy.Resolve(" Spanish ", "Français").Should().Be("Spanish");
      }

      // STAKE: an English game keeps the mirror it always had, so a player who writes in another tongue is still
      // answered in it.
      [Test]
      public void GIVEN_an_english_game_and_no_setting_WHEN_the_language_is_resolved_THEN_none_is_forced()
      {
         ReplyLanguagePolicy.Resolve("", "English").Should().BeEmpty();
         ReplyLanguagePolicy.Resolve(null, null).Should().BeEmpty();
      }

      // STAKE: every language the game offers reaches the model by a name it knows; a mod's language is kept as is.
      [TestCase("Deutsch", "German")]
      [TestCase("Русский", "Russian")]
      [TestCase("简体中文", "Simplified Chinese")]
      [TestCase("Español (LA)", "Latin American Spanish")]
      [TestCase("Português (BR)", "Brazilian Portuguese")]
      [TestCase("Català", "Català")]
      public void GIVEN_a_game_language_WHEN_resolved_THEN_the_model_gets_a_name_it_knows(string game, string expected)
      {
         ReplyLanguagePolicy.Resolve("", game).Should().Be(expected);
      }

      // STAKE: the opening a character speaks first names the language at its end, the last thing the model reads.
      [Test]
      public void GIVEN_a_language_WHEN_an_opening_is_told_THEN_it_ends_naming_the_language()
      {
         ReplyLanguagePolicy.WithLanguage("[Open the conversation yourself.]", "French").Should().EndWith("(Speak and narrate in French.)");
      }

      // STAKE: with no language, the opening is unchanged (an English game behaves as before).
      [Test]
      public void GIVEN_no_language_WHEN_an_opening_is_told_THEN_it_is_unchanged()
      {
         ReplyLanguagePolicy.WithLanguage("[Open.]", "").Should().Be("[Open.]");
      }
   }
}
