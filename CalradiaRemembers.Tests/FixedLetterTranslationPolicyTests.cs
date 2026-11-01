// Code written by Gabriel Mailhot, 31/10/2026.
// RaveN017Mod (Nexus PM, 29/10/2026): threats from lords "appear in English even though my game is in French". This decides
// which letters are translated when opened, and whether what came back may stand. If it is wrong, a French player reads
// English, an English player pays for a call that changes nothing, or a blackmail's sum differs from what the game will do.

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class FixedLetterTranslationPolicyTests
    {
        private static PendingLetter Letter(LetterReason reason, string content = "Keep from my path.") => new() {Reason = reason, Content = content};

        // STAKE: these are the five kinds written from a fixed English text; one left out stays English in a French game.
        [TestCase(LetterReason.StanceHostility)]
        [TestCase(LetterReason.StanceFavor)]
        [TestCase(LetterReason.Blackmail)]
        [TestCase(LetterReason.BastardMotherNote)]
        [TestCase(LetterReason.CompanionFarewell)]
        public void GIVEN_a_french_player_WHEN_a_fixed_letter_is_opened_THEN_it_is_translated(LetterReason reason)
        {
            FixedLetterTranslationPolicy.NeedsTranslation(Letter(reason), "French").Should().BeTrue();
        }

        // STAKE: a letter the model wrote is already in the player's language, and another mod's text is that mod's; a
        // translation of either is a wasted call that can only damage it.
        [TestCase(LetterReason.RealmTidings)]
        [TestCase(LetterReason.FromAnotherMod)]
        public void GIVEN_a_letter_not_written_from_a_fixed_text_WHEN_it_is_opened_THEN_it_is_left_alone(LetterReason reason)
        {
            FixedLetterTranslationPolicy.NeedsTranslation(Letter(reason), "French").Should().BeFalse();
        }

        // STAKE: Gabriel's approval, no call for an English player. A blank language is what an English game resolves to, and
        // Reply Language is a free text box.
        [TestCase("")]
        [TestCase(null)]
        [TestCase("English")]
        [TestCase(" english ")]
        [TestCase("Anglais")]
        public void GIVEN_an_english_player_WHEN_a_fixed_letter_is_opened_THEN_no_call_is_spent(string language)
        {
            FixedLetterTranslationPolicy.NeedsTranslation(Letter(LetterReason.StanceHostility), language).Should().BeFalse();
        }

        // STAKE: an envelope has no text to translate yet; the model writes it in the right language when it is opened.
        [Test]
        public void GIVEN_a_letter_with_no_text_WHEN_it_is_opened_THEN_there_is_nothing_to_translate()
        {
            FixedLetterTranslationPolicy.NeedsTranslation(Letter(LetterReason.StanceHostility, null), "French").Should().BeFalse();
        }

        // STAKE: the report: the French text is what the player reads.
        [Test]
        public void GIVEN_a_translation_WHEN_the_letter_is_shown_THEN_the_player_reads_it()
        {
            FixedLetterTranslationPolicy.TextToShow("Keep from my path.", "Passez votre chemin.").Should().Be("Passez votre chemin.");
        }

        // STAKE: Gabriel's approval: if the call fails the letter still reaches the player, in English.
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void GIVEN_no_translation_came_back_WHEN_the_letter_is_shown_THEN_the_player_reads_the_english(string translated)
        {
            FixedLetterTranslationPolicy.TextToShow("Keep from my path.", translated).Should().Be("Keep from my path.");
        }

        // STAKE: M-G6, a blackmail's prose must match the real mechanic. A sum or a delay that moved tells the player what the
        // game will not do, so English beats a wrong figure.
        [TestCase("Envoyez-moi 500 deniers avant 2 saisons.")]
        [TestCase("Envoyez-moi 5000 deniers avant deux saisons.")]
        public void GIVEN_a_translation_that_lost_a_number_WHEN_the_letter_is_shown_THEN_the_player_reads_the_english(string translated)
        {
            const string original = "Send me 5000 denars before 2 seasons are out.";

            FixedLetterTranslationPolicy.TextToShow(original, translated).Should().Be(original);
        }

        // STAKE: French writes "5 000" and German "5.000". The same sum in a language's own spelling is not a lost number,
        // or a good translation would be thrown away for English.
        [TestCase("Envoyez-moi 5 000 deniers.")]
        [TestCase("Envoyez-moi 5 000 deniers.")]
        [TestCase("Schickt mir 5.000 Denare.")]
        public void GIVEN_a_sum_in_a_languages_own_spelling_WHEN_the_letter_is_shown_THEN_the_translation_stands(string translated)
        {
            FixedLetterTranslationPolicy.TextToShow("Send me 5000 denars.", translated).Should().Be(translated);
        }

        // STAKE: models wrap an answer in quotation marks despite being told not to; the letter window would print them.
        [Test]
        public void GIVEN_a_translation_in_quotation_marks_WHEN_the_letter_is_shown_THEN_they_are_gone()
        {
            FixedLetterTranslationPolicy.TextToShow("Keep from my path.", "\"Passez votre chemin.\"").Should().Be("Passez votre chemin.");
        }
    }
}
