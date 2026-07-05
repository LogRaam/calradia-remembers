// Code written by Gabriel Mailhot, 05/07/2026.
// Documents the diacritic-folding fix: a player typing "Heloise" for a hero named "Héloïse the Finger" was a
// silent name-match failure. Fold() is the single pure seam both name-matching sites now share.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class NameNormalizationTests
    {
        [Test]
        public void GIVEN_an_accented_name_WHEN_folding_THEN_diacritics_are_stripped_and_lowercased()
        {
            NameNormalization.Fold("Héloïse").Should().Be("heloise");
        }

        [Test]
        public void GIVEN_an_already_plain_name_WHEN_folding_THEN_it_is_only_lowercased()
        {
            NameNormalization.Fold("Gabriel").Should().Be("gabriel");
        }

        [Test]
        public void GIVEN_mixed_case_input_WHEN_folding_THEN_the_result_is_fully_lowercase()
        {
            NameNormalization.Fold("HeLoIsE").Should().Be("heloise");
        }

        [Test]
        public void GIVEN_null_or_empty_input_WHEN_folding_THEN_an_empty_string_is_returned()
        {
            NameNormalization.Fold(null).Should().Be("");
            NameNormalization.Fold("").Should().Be("");
        }

        [Test]
        public void GIVEN_diacritics_from_several_languages_WHEN_folding_THEN_each_is_stripped()
        {
            NameNormalization.Fold("François").Should().Be("francois"); // ç
            NameNormalization.Fold("Zoë").Should().Be("zoe"); // ë
            NameNormalization.Fold("Iñigo").Should().Be("inigo"); // ñ
            NameNormalization.Fold("Björn").Should().Be("bjorn"); // ö
            NameNormalization.Fold("Pärttyli").Should().Be("parttyli"); // ä
        }

        [Test]
        public void GIVEN_a_full_epithet_name_WHEN_folding_THEN_spaces_and_word_order_are_preserved()
        {
            NameNormalization.Fold("Héloïse the Finger").Should().Be("heloise the finger");
        }
    }
}
