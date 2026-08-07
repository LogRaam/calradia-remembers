// Code written by Gabriel Mailhot, 06/08/2026.
// StylePersistencePolicy is the pure half of the v1.34.3 fix for a player report: the chat-picked narrative
// style reset to "Default" on every game restart and every mod update, because the mod's StyleService held
// the selection only in a static field with no persistence. The fix writes the chosen NAME to a small file
// in the player's Bannerlord user data and restores it at startup; this policy turns the raw file bytes
// (which may be missing, blank, multi-line, or hand-edited into a stale name) into the canonical style name
// to restore, validated against the styles actually on disk. Get it wrong and either the persisted choice is
// lost anyway (the bug returns) or a corrupt/stale file blanks the voice or resurrects a deleted style.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Styles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Styles
{
    [TestFixture]
    public sealed class StylePersistencePolicyTests
    {
        // STAKE: the whole point of the fix. A previously picked style that still exists on disk must come
        // back after a restart, case-canonicalised to the file's real spelling so the engine opens it.
        [Test]
        public void GIVEN_a_persisted_name_still_on_disk_WHEN_resolving_THEN_it_is_restored_canonically()
        {
            List<string> available = new List<string> {"Gritty-Noir", "Whimsical", "Default"};

            StylePersistencePolicy.ResolvePersistedName("gritty-noir", available).Should().Be("Gritty-Noir");
        }

        // STAKE: first run (no file was ever written) must not blank the voice; a null read degrades to the
        // known default exactly as if nothing had been picked.
        [Test]
        public void GIVEN_no_persistence_file_WHEN_resolving_THEN_the_default_is_used()
        {
            List<string> available = new List<string> {"Whimsical", "Default"};

            StylePersistencePolicy.ResolvePersistedName(null, available).Should().Be("Default");
        }

        // STAKE: a hand-edited or truncated file (blank, or only whitespace and newlines) must never throw or
        // select an empty style; it falls back to the default.
        [Test]
        public void GIVEN_a_blank_persisted_file_WHEN_resolving_THEN_the_default_is_used()
        {
            List<string> available = new List<string> {"Whimsical", "Default"};

            StylePersistencePolicy.ResolvePersistedName("   \r\n  ", available).Should().Be("Default");
        }

        // STAKE: a style the player picked and then deleted or renamed (or a file left behind after a mod
        // update removed that style) must degrade to the default, not keep naming a style that is gone.
        [Test]
        public void GIVEN_a_persisted_name_no_longer_on_disk_WHEN_resolving_THEN_the_default_is_used()
        {
            List<string> available = new List<string> {"Whimsical", "Default"};

            StylePersistencePolicy.ResolvePersistedName("Gritty-Noir-Deleted", available).Should().Be("Default");
        }

        // STAKE: the file is written by the mod but could be touched by a text editor that adds a trailing
        // newline or a stray second line; only the first real line is the name, so the match still works.
        [Test]
        public void GIVEN_a_file_with_a_trailing_newline_or_extra_lines_WHEN_resolving_THEN_only_the_first_line_is_the_name()
        {
            List<string> available = new List<string> {"Whimsical", "Default"};

            StylePersistencePolicy.ResolvePersistedName("Whimsical\r\n# a stray comment\r\n", available)
                                  .Should().Be("Whimsical");
        }

        // STAKE: what gets written must round-trip. Serialising the chosen name and reading it back must
        // restore that same name, or the persistence silently loses the pick.
        [Test]
        public void GIVEN_a_chosen_name_WHEN_serialized_and_resolved_back_THEN_the_same_name_is_restored()
        {
            List<string> available = new List<string> {"Novelistic", "Default"};

            string onDisk = StylePersistencePolicy.Serialize("Novelistic");
            StylePersistencePolicy.ResolvePersistedName(onDisk, available).Should().Be("Novelistic");
        }

        // STAKE: a blank or null name must never be written as an empty file that would later read back as
        // "no choice"; it collapses to the default so the file always names a real style.
        [Test]
        public void GIVEN_a_blank_name_WHEN_serialized_THEN_it_collapses_to_the_default()
        {
            StylePersistencePolicy.Serialize(null).Should().Be(StyleCatalogPolicy.DefaultStyleName);
            StylePersistencePolicy.Serialize("   ").Should().Be(StyleCatalogPolicy.DefaultStyleName);
        }
    }
}
