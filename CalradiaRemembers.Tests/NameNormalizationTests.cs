// Code written by Gabriel Mailhot, 05/07/2026.
// Documents the diacritic-folding fix: a player typing "Heloise" for a hero named "Héloïse the Finger" was a
// silent name-match failure. Fold() is the single pure seam both name-matching sites now share.
// Feeds ContextualNameResolver (folds message tokens and hero name parts alike, so the prompt's "NAMES
// MENTIONED" section actually finds the hero) and ModCheats' console-command hero resolver. A regression
// here reopens the exact silent-mismatch bug this file was written to close.

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
        // The direct fix for the reported failure: "Heloise" (as typed) must fold to the exact same
        // string as "Héloïse" (as stored), or the name-matching sites this seam feeds never see a match.
        [Test]
        public void GIVEN_an_accented_name_WHEN_folding_THEN_diacritics_are_stripped_and_lowercased()
        {
            NameNormalization.Fold("Héloïse").Should().Be("heloise");
        }

        // Guards that folding an already-plain name is a no-op beyond casing: most hero names in
        // Calradia carry no diacritics at all, so the common case must not be disturbed by the fix.
        [Test]
        public void GIVEN_an_already_plain_name_WHEN_folding_THEN_it_is_only_lowercased()
        {
            NameNormalization.Fold("Gabriel").Should().Be("gabriel");
        }

        // Player typing is not guaranteed to match a hero's stored casing; both sides of a match must
        // fold to the same case or an accidental capital would silently reopen the mismatch this class
        // exists to close.
        [Test]
        public void GIVEN_mixed_case_input_WHEN_folding_THEN_the_result_is_fully_lowercase()
        {
            NameNormalization.Fold("HeLoIsE").Should().Be("heloise");
        }

        // Fail SAFE into an empty string, never null: callers feed the result straight into further
        // string operations (HashSet.Contains, Split, interpolation) at both consuming call sites, so
        // a null here would throw deep inside a name-matching call rather than at the source.
        [Test]
        public void GIVEN_null_or_empty_input_WHEN_folding_THEN_an_empty_string_is_returned()
        {
            NameNormalization.Fold(null).Should().Be("");
            NameNormalization.Fold("").Should().Be("");
        }

        // The NFD-decompose-and-strip approach must generalize past the single accent in the bug
        // report (é/ï): pins five different diacritic marks across languages so a narrower fix (e.g.
        // a hardcoded "é to e" table) cannot quietly regress on names never personally hit in testing.
        [Test]
        public void GIVEN_diacritics_from_several_languages_WHEN_folding_THEN_each_is_stripped()
        {
            NameNormalization.Fold("François").Should().Be("francois"); // ç
            NameNormalization.Fold("Zoë").Should().Be("zoe"); // ë
            NameNormalization.Fold("Iñigo").Should().Be("inigo"); // ñ
            NameNormalization.Fold("Björn").Should().Be("bjorn"); // ö
            NameNormalization.Fold("Pärttyli").Should().Be("parttyli"); // ä
        }

        // Hero names in this mod are often full epithets ("Héloïse the Finger"); folding must strip
        // diacritics without collapsing spaces or reordering words, or the folded name would no longer
        // line up token-for-token against the message tokens it gets compared to.
        [Test]
        public void GIVEN_a_full_epithet_name_WHEN_folding_THEN_spaces_and_word_order_are_preserved()
        {
            NameNormalization.Fold("Héloïse the Finger").Should().Be("heloise the finger");
        }
    }
}
