// Code written by Gabriel Mailhot, 14/08/2026.
// Decides whether the parchment letter view swaps the decorative Galahad font for a plain Cyrillic-safe
// one (CR.Letter.Body/Heading/Input.Plain sibling brushes in GUI/Brushes/CalradiaRemembersLetter.xml,
// picked by LetterViewModel's BodyBrush/HeadingBrush/InputBrush bindings). Wrong here means either
// Russian keeps rendering Galahad's broken per-glyph fallback (the player report this policy exists to
// fix), or a Latin language loses its decorative parchment look for no reason.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class LetterFontPolicyTests
   {
      // The whole point of this policy: Galahad has no proper Cyrillic glyphs, so the id
      // TaleWorlds.Localization.MBTextManager.ActiveTextLanguage actually returns for Russian (verified
      // against Modules/Native/ModuleData/Languages/RU/language_data.xml, id="Русский") must switch the
      // letter view to the plain font.
      [Test]
      public void GIVEN_the_Russian_native_display_name_WHEN_deciding_THEN_it_prefers_the_plain_font()
      {
         LetterFontPolicy.PreferPlainFont("Русский").Should().BeTrue();
      }

      // Defensive alternate form: nothing in this codebase currently hands the policy the English word
      // "Russian" instead of the engine's native display name, but MBTextManager.ActiveTextLanguage is
      // the only verified source and a future caller could plausibly differ.
      [Test]
      public void GIVEN_the_English_word_Russian_WHEN_deciding_THEN_it_prefers_the_plain_font()
      {
         LetterFontPolicy.PreferPlainFont("Russian").Should().BeTrue();
      }

      // English is the game's DefaultLanguage (per NativeLanguages.xml) and Galahad has a full glyph set
      // for it; the decorative parchment look is the intended default and must not regress.
      [Test]
      public void GIVEN_English_WHEN_deciding_THEN_it_keeps_the_decorative_font()
      {
         LetterFontPolicy.PreferPlainFont("English").Should().BeFalse();
      }

      // French is Gabriel's own dev/QA language (CLAUDE.md: "Gabriel writes in French") and a Latin
      // script; it must keep the decorative look CR ships by default.
      [Test]
      public void GIVEN_French_WHEN_deciding_THEN_it_keeps_the_decorative_font()
      {
         LetterFontPolicy.PreferPlainFont("Français").Should().BeFalse();
      }

      // Case/whitespace defensiveness: the id is read off a live engine API, not hand-typed, but a
      // policy this cheap to make robust should not silently miss a match over stray casing/whitespace.
      [Test]
      public void GIVEN_a_differently_cased_and_padded_Russian_id_WHEN_deciding_THEN_it_still_matches()
      {
         LetterFontPolicy.PreferPlainFont("  русский  ").Should().BeTrue();
      }

      // Never let an unknown or missing language id silently break the Latin default rendering: falling
      // through to false (decorative Galahad) is always the safe failure mode, never the plain font.
      [Test]
      public void GIVEN_null_or_empty_or_an_unknown_language_id_WHEN_deciding_THEN_it_keeps_the_decorative_font()
      {
         LetterFontPolicy.PreferPlainFont(null).Should().BeFalse();
         LetterFontPolicy.PreferPlainFont("").Should().BeFalse();
         LetterFontPolicy.PreferPlainFont("Klingon").Should().BeFalse();
      }
   }
}
