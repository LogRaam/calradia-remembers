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
    }
}
