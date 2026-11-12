// Code written by Gabriel Mailhot, 07/09/2026.
// What this pins: an author's ability to write a character's romantic preferences by hand in
// character_overrides.json, and what happens when they mistype one.
//
// Why it exists. A player asked on 2026-09-07 whether monogamy or polyamory could be set per character. It could
// not. Preferences were rolled once from a trait-derived pool with no authoring surface at all, so the only lever
// was to lie about a trait, and once polyamory was correctly decoupled from honour and bound to the culture, even
// that crooked lever disappeared: an author who wanted one polyamorous character inside a strict culture had no
// way to say so. This field is that way, and it sits beside the orientation and kink locks that were added for
// the same reason before it.
//
// The parsing rules matter as much as the field. This file is hand-edited by players, so a typo must never crash
// a campaign, and must never vanish in silence either: an author who mistypes every entry would otherwise pin the
// character to NO preferences at all and be told nothing, which reads exactly like the mod ignoring them. That is
// the failure the project keeps returning to fix, so it is pinned here rather than trusted.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class AuthoredRomanticPreferenceTests
    {
        // The request itself: a player writes what they mean, in the words the enum uses, and gets it.
        [Test]
        public void GIVEN_authored_preference_names_WHEN_parsed_THEN_they_become_the_real_preferences()
        {
            RomanticPreferenceParser.Parse(new List<string> {"Polyamorous", "Devoted"})
                .Should().Equal(RomanticPreference.Polyamorous, RomanticPreference.Devoted);
        }

        // A hand-edited file is typed by a human at midnight: case and stray whitespace must not decide whether a
        // character comes out as the author wrote them.
        [Test]
        public void GIVEN_sloppy_casing_and_whitespace_WHEN_parsed_THEN_it_is_still_understood()
        {
            RomanticPreferenceParser.Parse(new List<string> {"  polyamorous  ", "DEVOTED"})
                .Should().Equal(RomanticPreference.Polyamorous, RomanticPreference.Devoted);
        }

        // Forgiving, never fatal: one bad line must not cost the author the good ones, and must not throw in the
        // middle of a campaign load.
        [Test]
        public void GIVEN_a_typo_among_good_names_WHEN_parsed_THEN_the_good_ones_survive_and_nothing_throws()
        {
            RomanticPreferenceParser.Parse(new List<string> {"Polyamorus", "Devoted"})
                .Should().Equal(RomanticPreference.Devoted);
        }

        // Forgiving is not the same as silent. A lock of nothing but typos pins the character to ZERO preferences,
        // which is indistinguishable from a deliberate empty lock unless the mod says so out loud. This is the
        // exact failure a player reported for kinks and orientation before it.
        [Test]
        public void GIVEN_a_typo_WHEN_asking_what_went_wrong_THEN_the_offending_word_is_named()
        {
            RomanticPreferenceParser.Unrecognized(new List<string> {"Polyamorus", "Devoted"})
                .Should().Equal("Polyamorus");
        }

        // None is a placeholder in the enum, not something a character can be. It parses, so calling it a typo
        // would be wrong, but writing it achieves nothing, and a quiet nothing is what this project keeps having
        // to apologise for. It is reported rather than swallowed.
        [Test]
        public void GIVEN_the_placeholder_None_WHEN_authored_THEN_it_is_ignored_and_reported()
        {
            RomanticPreferenceParser.Parse(new List<string> {"None"}).Should().BeEmpty();
            RomanticPreferenceParser.Unrecognized(new List<string> {"None"}).Should().Equal("None");
        }

        // Omitting the field and writing an empty list mean opposite things, and the caller decides what to do
        // with each: null keeps whatever was rolled, empty is a deliberate lock to nothing. Collapsing the two
        // would silently strip the preferences of every character in a file that never mentions them.
        [Test]
        public void GIVEN_the_field_is_omitted_WHEN_read_THEN_it_is_null_where_an_empty_list_is_a_real_lock()
        {
            new CharacterOverride {Hero = "Ira"}.ParsedPreferences.Should().BeNull();
            new CharacterOverride {Hero = "Ira", Preferences = new List<string>()}.ParsedPreferences.Should().BeEmpty();
        }

        // An entry whose ONLY content is a preference lock is still authored content. A character defined with
        // nothing else must not be skipped as an empty entry: that precise bug was reported for a missing kinks
        // field, fixed, then reported again for a misspelled orientation.
        [Test]
        public void GIVEN_an_entry_with_only_a_preference_WHEN_asked_THEN_it_counts_as_authored_content()
        {
            new CharacterOverride {Hero = "Ira", Preferences = new List<string> {"Polyamorous"}}
                .HasAnyAuthoredContent.Should().BeTrue();
        }

        // EvilPoptart1987 (Nexus, 12/11/2026): "some edits work, but 'preferences' do not update." STAKE: an entry that
        // changes only the preferences (no traits, no kinks) was never laid on the stored profile, so cr.reload_lore and
        // cr.prompt_dump kept the old ones.
        [Test]
        public void GIVEN_an_entry_with_only_a_preference_WHEN_a_reload_asks_what_to_lay_on_the_stored_profile_THEN_the_preferences_are_laid()
        {
            new CharacterOverride {Hero = "Ira", Preferences = new List<string> {"Polyamorous"}}.LocksStoredTastes.Should().BeTrue();
        }

        // STAKE: a deliberate lock to no preferences at all is a lock too, and must reach the stored profile.
        [Test]
        public void GIVEN_an_empty_preference_lock_WHEN_asked_THEN_it_is_still_laid()
        {
            new CharacterOverride {Hero = "Ira", Preferences = new List<string>()}.LocksStoredTastes.Should().BeTrue();
        }

        // STAKE: an entry that leaves both preferences and gestures out must not rewrite the character's own roll.
        [Test]
        public void GIVEN_an_entry_with_only_a_backstory_WHEN_asked_THEN_nothing_is_laid_on_the_stored_tastes()
        {
            new CharacterOverride {Hero = "Ira", Backstory = "A widow of the hills."}.LocksStoredTastes.Should().BeFalse();
        }

        // The author's own diagnostics (cr.lore_status, the reload log) must name a bad preference the same way
        // they already name a bad kink or orientation, or the new field is the one place the file fails quietly.
        [Test]
        public void GIVEN_a_bad_preference_WHEN_listing_authoring_problems_THEN_it_is_reported_like_the_others()
        {
            new CharacterOverride {Hero = "Ira", Preferences = new List<string> {"Polyamorus"}}
                .AuthoringProblems().Should().ContainSingle(p => p.Contains("Polyamorus"));
        }
    }
}
