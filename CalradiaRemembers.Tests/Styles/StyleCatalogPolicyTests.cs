// Code written by Gabriel Mailhot, 26/07/2026.
// StyleCatalogPolicy decides which narrative style NAME actually applies, given what the player asked for
// (from the chat-window popup) and the style .txt files that exist under ModuleData/Styles. Consumed by
// the mod's StyleService, which picks the active style file with this policy and then decides (via
// StyleCachePolicy) whether to re-read it before building the prompt. Get the name resolution wrong and
// either a deleted/renamed style file silently blanks the narrative voice instead of degrading to a known
// default, or the engine opens the wrong file because the popup showed a casing that does not match disk.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Styles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Styles
{
    [TestFixture]
    public sealed class StyleCatalogPolicyTests
    {
        // STAKE: with no Styles folder (or an empty one) the prompt must still build with a known name
        // rather than an empty voice.
        [Test]
        public void GIVEN_no_style_files_available_WHEN_resolving_THEN_the_default_name_is_returned()
        {
            StyleCatalogPolicy.ResolveName("Anything", null).Should().Be(StyleCatalogPolicy.DefaultStyleName);
            StyleCatalogPolicy.ResolveName("Anything", new List<string>()).Should().Be(StyleCatalogPolicy.DefaultStyleName);
        }

        // STAKE: the popup and the log must show the real file name, and the engine must open the right
        // file, so a case-insensitive match still resolves to the file's OWN spelling, not the request.
        [Test]
        public void GIVEN_a_requested_name_matching_a_file_case_insensitively_WHEN_resolving_THEN_the_files_canonical_spelling_is_returned()
        {
            List<string> available = new List<string> {"Gritty-Noir", "Whimsical", "Default"};

            StyleCatalogPolicy.ResolveName("gritty-noir", available).Should().Be("Gritty-Noir");
            StyleCatalogPolicy.ResolveName("WHIMSICAL", available).Should().Be("Whimsical");
        }

        // STAKE: a style file deleted or renamed while selected must degrade to the default voice, not
        // break the conversation.
        [Test]
        public void GIVEN_an_unknown_requested_name_WHEN_a_default_file_exists_THEN_its_canonical_spelling_is_returned()
        {
            List<string> available = new List<string> {"Whimsical", "DEFAULT"};

            StyleCatalogPolicy.ResolveName("Gritty-Noir-No-Longer-There", available).Should().Be("DEFAULT");
        }

        // STAKE: pins the last-resort value the engine treats as "no style file", for the case where even
        // a default file is absent from disk.
        [Test]
        public void GIVEN_an_unknown_requested_name_WHEN_no_default_file_exists_THEN_the_default_style_name_constant_is_returned()
        {
            List<string> available = new List<string> {"Whimsical", "Gritty-Noir"};

            StyleCatalogPolicy.ResolveName("Something-Unknown", available).Should().Be(StyleCatalogPolicy.DefaultStyleName);
        }

        // STAKE: nothing selected yet (fresh session, no popup pick made) must resolve to the default,
        // the same as any other unknown request.
        [Test]
        public void GIVEN_a_null_or_whitespace_request_WHEN_resolving_THEN_it_behaves_like_an_unknown_request()
        {
            List<string> available = new List<string> {"Whimsical", "Default"};

            StyleCatalogPolicy.ResolveName(null, available).Should().Be("Default");
            StyleCatalogPolicy.ResolveName("   ", available).Should().Be("Default");
        }

        // STAKE: the shipped README.txt appeared in the player's style picker as if it were a style
        // (player report 2026-07-26); it must be filtered out regardless of casing.
        [Test]
        public void GIVEN_the_readme_file_WHEN_checking_selectability_THEN_it_is_not_selectable()
        {
            StyleCatalogPolicy.IsSelectableStyleFile("README").Should().BeFalse();
            StyleCatalogPolicy.IsSelectableStyleFile("readme").Should().BeFalse();
        }

        // STAKE: pins the documented underscore convention so a modder can park a draft or template in
        // the folder without it polluting the picker.
        [Test]
        public void GIVEN_a_leading_underscore_WHEN_checking_selectability_THEN_it_is_not_selectable()
        {
            StyleCatalogPolicy.IsSelectableStyleFile("_WorkInProgress").Should().BeFalse();
        }

        // STAKE: the filter must not swallow real styles.
        [Test]
        public void GIVEN_an_ordinary_style_name_WHEN_checking_selectability_THEN_it_is_selectable()
        {
            StyleCatalogPolicy.IsSelectableStyleFile("Novelistic").Should().BeTrue();
        }

        // STAKE: a malformed entry must never reach the picker as a blank row.
        [Test]
        public void GIVEN_a_null_or_whitespace_name_WHEN_checking_selectability_THEN_it_is_not_selectable()
        {
            StyleCatalogPolicy.IsSelectableStyleFile(null).Should().BeFalse();
            StyleCatalogPolicy.IsSelectableStyleFile("   ").Should().BeFalse();
        }

        // MergeStyleNames backs the Custom/ override folder (mirrors ModuleData/Custom for prompts): a
        // player's own style file under ModuleData/Custom/Styles must override a shipped one of the same
        // name and both sets must merge without duplicates, so a customization survives a mod update and
        // is not shown twice in the picker.

        // STAKE: without this, a player who copies "Novelistic.txt" into Custom/Styles to tweak it would
        // see "Novelistic" listed twice in the picker, one shipped and one custom, with no way to tell
        // which is which.
        [Test]
        public void GIVEN_a_custom_name_matching_a_shipped_name_WHEN_merging_THEN_only_one_entry_is_returned()
        {
            List<string> shipped = new List<string> {"Novelistic", "Terse"};
            List<string> custom = new List<string> {"Novelistic"};

            StyleCatalogPolicy.MergeStyleNames(shipped, custom).Should().Equal("Novelistic", "Terse");
        }

        // STAKE: a custom style with a name nobody shipped (the common case, a player's own invented
        // voice) must still show up in the picker alongside the shipped ones, not replace them.
        [Test]
        public void GIVEN_disjoint_shipped_and_custom_names_WHEN_merging_THEN_the_union_is_returned()
        {
            List<string> shipped = new List<string> {"Default", "Terse"};
            List<string> custom = new List<string> {"MyVoice"};

            StyleCatalogPolicy.MergeStyleNames(shipped, custom).Should().Equal("Default", "MyVoice", "Terse");
        }

        // STAKE: file systems and players are not consistent about casing; "novelistic" in Custom must
        // still be recognized as overriding "Novelistic" shipped, not treated as a second, unrelated style.
        [Test]
        public void GIVEN_a_custom_name_differing_only_in_case_WHEN_merging_THEN_it_still_dedupes_to_one_entry()
        {
            List<string> shipped = new List<string> {"Novelistic"};
            List<string> custom = new List<string> {"NOVELISTIC"};

            StyleCatalogPolicy.MergeStyleNames(shipped, custom).Should().ContainSingle();
        }

        // STAKE: most players never create a Custom/Styles folder at all, so the everyday case (no
        // custom styles) must reduce to exactly the shipped list, unchanged.
        [Test]
        public void GIVEN_no_custom_names_WHEN_merging_THEN_the_shipped_names_are_returned_unchanged()
        {
            List<string> shipped = new List<string> {"Default", "Grimdark"};

            StyleCatalogPolicy.MergeStyleNames(shipped, new List<string>()).Should().Equal("Default", "Grimdark");
        }

        // STAKE: the picker relies on alphabetical order regardless of which folder a name came from, so
        // a custom-only or mixed-case result must not appear out of order.
        [Test]
        public void GIVEN_mixed_shipped_and_custom_names_WHEN_merging_THEN_the_result_is_sorted_case_insensitively()
        {
            List<string> shipped = new List<string> {"Whimsical", "default"};
            List<string> custom = new List<string> {"Austen"};

            StyleCatalogPolicy.MergeStyleNames(shipped, custom).Should().Equal("Austen", "default", "Whimsical");
        }
    }
}
