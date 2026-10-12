// Code written by Gabriel Mailhot, 12/10/2026.
// The Prompt Composer, increment 2 (docs/design/prompt-composer/DESIGN.md; Gabriel, 11/10/2026: "Un param MCM pourrait
// designer l'ordonnanceur a utiliser, car il pourrait y avoir plusieurs mods qui proposent un ordonnanceur", "Mise en
// page par type"). Decides which prompt profiles exist, what a profile's files say, which layout a character gets, and
// which profile stays chosen in MCM. If it is wrong: a mod's profile is never offered, a player's is wiped by an update,
// a broken file breaks every conversation instead of falling back to stock, a lord is given a spouse's layout, or adding
// a mod silently switches the player to another mod's prompt.

#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PromptProfilesTests
    {
        // ── Which kind of character ─────────────────────────────────────────

        // STAKE: DESIGN.md, the kinds most specific first: a spouse is a spouse before being a lord or a companion.
        [Test]
        public void GIVEN_the_players_spouse_who_is_also_a_lord_WHEN_the_kind_is_decided_THEN_it_is_spouse()
        {
            PromptKindPolicy.Of(new PromptKindFacts {IsPlayerSpouse = true, IsLord = true}).Should().Be(PromptKinds.Spouse);
        }

        // STAKE: a companion in a captive scene keeps the companion layout (companion comes before captive_scene).
        [Test]
        public void GIVEN_a_companion_in_a_captive_scene_WHEN_the_kind_is_decided_THEN_it_is_companion()
        {
            PromptKindPolicy.Of(new PromptKindFacts {IsPlayerCompanion = true, InCaptiveScene = true}).Should().Be(PromptKinds.Companion);
        }

        // STAKE: the stations CR already tells apart for the behaviour guidelines decide the rest, so a layout follows the
        // same register the character speaks in.
        [Test]
        public void GIVEN_each_station_WHEN_the_kind_is_decided_THEN_it_follows_the_station()
        {
            PromptKindPolicy.Of(new PromptKindFacts {InCaptiveScene = true, IsLord = true}).Should().Be(PromptKinds.CaptiveScene);
            PromptKindPolicy.Of(new PromptKindFacts {StationKey = StationGuidelinesPolicy.GangLeaderKey}).Should().Be(PromptKinds.GangLeader);
            PromptKindPolicy.Of(new PromptKindFacts {StationKey = StationGuidelinesPolicy.NotableKey}).Should().Be(PromptKinds.Notable);
            PromptKindPolicy.Of(new PromptKindFacts {StationKey = StationGuidelinesPolicy.WandererKey}).Should().Be(PromptKinds.Wanderer);
            PromptKindPolicy.Of(new PromptKindFacts {IsLord = true}).Should().Be(PromptKinds.Lord);
            PromptKindPolicy.Of(new PromptKindFacts()).Should().Be(PromptKinds.Default);
        }

        // ── Which profiles exist ────────────────────────────────────────────

        // STAKE: stock is always offered first (the reset is one MCM switch away); the player's own profiles live under
        // Custom, which CR updates never touch; each mod's come in the launcher order the player controls.
        [Test]
        public void GIVEN_custom_and_two_mods_with_profiles_WHEN_listed_THEN_stock_then_the_players_then_each_mods()
        {
            var dirs = new Dictionary<string, string[]> {
                [Path.Combine(@"D:\MyCR", "Prompts")] = new[] {Path.Combine(@"D:\MyCR", "Prompts", "terse")},
                [Path.Combine(@"G\Bellum", "ModuleData", "cr_prompts")] = new[] {Path.Combine(@"G\Bellum", "ModuleData", "cr_prompts", "bellum_court")}
            };

            IReadOnlyList<PromptProfileSource> found = PromptProfileFolders.List(@"D:\MyCR", new[] {("Bellum", @"G\Bellum"), ("Empty", @"G\Empty")},
                "CalradiaRemembers", d => dirs.TryGetValue(d, out string[] s) ? s : new string[0], null);

            found.Select(p => p.Id).Should().Equal(PromptProfileFolders.StockId, "terse", "bellum_court");
            found.Select(p => p.Source).Should().Equal("CalradiaRemembers", "Custom", "Bellum");
        }

        // STAKE: two profiles with one id would make the MCM choice ambiguous: the first found keeps it, the other is
        // reported, and a profile may never take stock's id.
        [Test]
        public void GIVEN_two_profiles_with_one_id_WHEN_listed_THEN_the_first_keeps_it_and_the_other_is_reported()
        {
            var dirs = new Dictionary<string, string[]> {
                [Path.Combine(@"D:\MyCR", "Prompts")] = new[] {Path.Combine(@"D:\MyCR", "Prompts", "terse"), Path.Combine(@"D:\MyCR", "Prompts", "stock")},
                [Path.Combine(@"G\Bellum", "ModuleData", "cr_prompts")] = new[] {Path.Combine(@"G\Bellum", "ModuleData", "cr_prompts", "terse")}
            };
            var problems = new List<string>();

            IReadOnlyList<PromptProfileSource> found = PromptProfileFolders.List(@"D:\MyCR", new[] {("Bellum", @"G\Bellum")}, "CalradiaRemembers",
                d => dirs.TryGetValue(d, out string[] s) ? s : new string[0], problems);

            found.Select(p => p.Id).Should().Equal(PromptProfileFolders.StockId, "terse");
            found[1].Source.Should().Be("Custom");
            problems.Should().HaveCount(2);
        }

        // ── What a profile says ─────────────────────────────────────────────

        private static PromptProfile Read(Dictionary<string, string> files, string id = "terse")
            => PromptProfileReader.Read(new PromptProfileSource {Id = id, Source = "Custom", Folder = @"P\" + id},
                path => files.TryGetValue(path, out string text) ? text : null,
                dir => files.Keys.Where(k => Path.GetDirectoryName(k) == dir));

        // STAKE: a profile's layout file is read as the player wrote it: the order of the prefix and tail, and the off list.
        [Test]
        public void GIVEN_a_profile_with_a_lord_layout_WHEN_read_THEN_its_order_and_off_list_are_taken()
        {
            PromptProfile p = Read(new Dictionary<string, string> {
                [@"P\terse\profile.json"] = "{\"name\": \"Terse\", \"parent\": \"\"}",
                [@"P\terse\layouts\lord.json"] = "{\"prefix\": [\"identity\", \"dialogue_style\"], \"tail\": [\"role_anchor\"], \"off\": [\"prose_craft\"]}"
            });

            p.Name.Should().Be("Terse");
            p.Layouts[PromptKinds.Lord].Prefix.Should().Equal("identity", "dialogue_style");
            p.Layouts[PromptKinds.Lord].Off.Should().Equal("prose_craft");
            p.Problems.Should().BeEmpty();
        }

        // STAKE: a broken file must not break conversations: the layout is left out and the problem named, so the
        // character falls back to the next layout (and finally stock).
        [Test]
        public void GIVEN_a_layout_file_that_is_not_json_WHEN_read_THEN_it_is_left_out_and_reported()
        {
            PromptProfile p = Read(new Dictionary<string, string> {[@"P\terse\layouts\lord.json"] = "{ prefix: [ oops"});

            p.Layouts.Should().NotContainKey(PromptKinds.Lord);
            p.Problems.Should().ContainSingle(x => x.Contains("lord.json"));
        }

        // STAKE: a layout named after no kind (a typo, "lords.json") would never be used: it is reported, not silently kept.
        [Test]
        public void GIVEN_a_layout_named_after_no_kind_WHEN_read_THEN_it_is_reported()
        {
            PromptProfile p = Read(new Dictionary<string, string> {[@"P\terse\layouts\lords.json"] = "{\"prefix\": []}"});

            p.Layouts.Should().BeEmpty();
            p.Problems.Should().ContainSingle(x => x.Contains("lords"));
        }

        // ── What a profile rewords (increment 3) ────────────────────────────

        // STAKE: DESIGN.md: a section file REPLACES the stock text by default; "!#" lines are notes the model never sees.
        [Test]
        public void GIVEN_a_section_file_WHEN_read_THEN_its_words_replace_the_stock_text_and_notes_are_dropped()
        {
            PromptProfile p = Read(new Dictionary<string, string> {
                [@"P\terse\sections\dialogue_style.txt"] = "!# based_on: dialogue_style@abc\nSpeak tersely.\nNo flourishes."
            });

            p.Sections["dialogue_style"].Text.Should().Be("Speak tersely.\nNo flourishes.");
            p.Sections["dialogue_style"].Append.Should().BeFalse();
            p.SectionBasedOn["dialogue_style"].Should().Be("dialogue_style@abc");
        }

        // STAKE: "!append" keeps CR's text and adds the player's; "!remove" drops only the lines named.
        [Test]
        public void GIVEN_append_and_remove_directives_WHEN_read_THEN_they_are_taken()
        {
            PromptProfile p = Read(new Dictionary<string, string> {
                [@"P\terse\sections\prose_craft.txt"] = "!append\nNever rhyme.",
                [@"P\terse\sections\identity.txt"] = "!remove purple prose\n!remove ornate"
            });

            p.Sections["prose_craft"].Append.Should().BeTrue();
            p.Sections["prose_craft"].Text.Should().Be("Never rhyme.");
            p.Sections["identity"].Text.Should().BeNull();
            p.Sections["identity"].RemoveLines.Should().Equal("purple prose", "ornate");
        }

        // STAKE: the nearest profile's rewording wins, then the parent's: a small tweak over an overhaul rewords on top.
        [Test]
        public void GIVEN_a_profile_and_its_parent_both_rewording_WHEN_merged_THEN_the_nearest_wins_and_the_rest_comes_from_the_parent()
        {
            var tweak = new PromptProfile {Id = "tweak", Sections = {["identity"] = new PromptSectionText {Text = "mine"}}};
            var baseProfile = new PromptProfile {Id = "base", Sections = {["identity"] = new PromptSectionText {Text = "theirs"}, ["prose_craft"] = new PromptSectionText {Text = "craft"}}};

            IReadOnlyDictionary<string, PromptSectionText> texts = PromptSectionTexts.Of(new[] {tweak, baseProfile});

            texts["identity"].Text.Should().Be("mine");
            texts["prose_craft"].Text.Should().Be("craft");
            PromptSectionTexts.Of(new PromptProfile[0]).Should().BeNull();
        }

        // ── Which layout a character gets ───────────────────────────────────

        private static PromptProfile Profile(string id, string parent, params string[] kinds)
            => new() {Id = id, Parent = parent, Layouts = kinds.ToDictionary(k => k, k => new PromptLayout {Source = id + ":" + k})};

        // STAKE: DESIGN.md: the profile's own layout for the kind, else its default, then its parent's, then stock (null).
        [Test]
        public void GIVEN_a_profile_and_its_parent_WHEN_a_layout_is_chosen_THEN_kind_then_default_then_the_parents_then_stock()
        {
            var chain = new List<PromptProfile> {Profile("tweak", "base", PromptKinds.Spouse), Profile("base", null, PromptKinds.Lord, PromptKinds.Default)};

            PromptLayoutChoice.For(PromptKinds.Spouse, chain).Source.Should().Be("tweak:spouse");
            PromptLayoutChoice.For(PromptKinds.Lord, chain).Source.Should().Be("base:lord");
            PromptLayoutChoice.For(PromptKinds.Notable, chain).Source.Should().Be("base:default");
            PromptLayoutChoice.For(PromptKinds.Notable, new List<PromptProfile> {Profile("tweak", null, PromptKinds.Spouse)}).Should().BeNull();
        }

        // STAKE: a parent that does not exist, or a loop of parents, must end in stock rather than hang or throw.
        [Test]
        public void GIVEN_a_missing_parent_and_a_loop_WHEN_the_chain_is_built_THEN_it_stops_and_reports()
        {
            var problems = new List<string>();
            var profiles = new Dictionary<string, PromptProfile> {
                ["a"] = Profile("a", "b"), ["b"] = Profile("b", "a"), ["c"] = Profile("c", "gone")
            };

            PromptProfileChain.Of("a", profiles, problems).Select(p => p.Id).Should().Equal("a", "b");
            PromptProfileChain.Of("c", profiles, problems).Select(p => p.Id).Should().Equal("c");
            PromptProfileChain.Of("nowhere", profiles, problems).Should().BeEmpty();
            problems.Should().HaveCount(3);
        }

        // ── Which profile stays chosen ──────────────────────────────────────

        // STAKE: the player picks in MCM, and that pick is the one used.
        [Test]
        public void GIVEN_the_same_list_as_last_time_WHEN_the_player_picked_a_profile_THEN_that_pick_is_used()
        {
            PromptProfileSelection.Resolve(new PromptSelectionFacts {
                ListNow = new[] {"stock", "terse", "bellum_court"}, SelectedIndexNow = 2,
                ListLastTime = new[] {"stock", "terse", "bellum_court"}, ChosenLastTime = "terse"
            }).ChosenId.Should().Be("bellum_court");
        }

        // STAKE: MCM saves only the place in the list. A mod added since moves every place, and the same place would now
        // name another mod's prompt: the profile chosen last time is found again by its id.
        [Test]
        public void GIVEN_a_mod_added_a_profile_since_last_time_WHEN_resolved_THEN_the_profile_chosen_last_time_stays_chosen()
        {
            PromptSelection s = PromptProfileSelection.Resolve(new PromptSelectionFacts {
                ListNow = new[] {"stock", "terse", "aaa_new", "bellum_court"}, SelectedIndexNow = 2,
                ListLastTime = new[] {"stock", "terse", "bellum_court"}, ChosenLastTime = "bellum_court"
            });

            s.ChosenId.Should().Be("bellum_court");
            s.SelectedIndex.Should().Be(3);
        }

        // STAKE: the chosen profile's mod was removed: stock, said in the log, never a stranger's prompt.
        [Test]
        public void GIVEN_the_chosen_profile_is_gone_WHEN_resolved_THEN_it_falls_back_to_stock_with_a_notice()
        {
            PromptSelection s = PromptProfileSelection.Resolve(new PromptSelectionFacts {
                ListNow = new[] {"stock", "terse"}, SelectedIndexNow = 1,
                ListLastTime = new[] {"stock", "terse", "bellum_court"}, ChosenLastTime = "bellum_court"
            });

            s.ChosenId.Should().Be("stock");
            s.SelectedIndex.Should().Be(0);
            s.Notice.Should().Contain("bellum_court");
        }
    }
}
