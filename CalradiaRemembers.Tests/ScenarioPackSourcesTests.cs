// Code written by Gabriel Mailhot, 11/10/2026.
// Decides where scenario packs are read from and which rule keeps an id when two packs share one. Gabriel (11/10/2026)
// asked that mods can ship scenarios, as they ship tales. If the folders are wrong, a player's pack is wiped by an update
// or a mod's pack is never read; if two rules keep the same id, firing one seals the other in the save (seals are kept by
// id alone), so a mod's rule could silently switch off one of CR's.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ScenarioPackSourcesTests
    {
        private static ScenarioRule Rule(string id)
            => ScenarioRule.Parse(id, "ON_DEATH", null, null, "a message", true, 1);

        // STAKE: CR's shipped packs come first and keep their ids; the player's own pack sits in the Custom folder, which
        // an update of CR never replaces; a mod's packs come last, in the launcher order the player already controls.
        [Test]
        public void GIVEN_cr_the_custom_folder_and_two_mods_WHEN_folders_are_listed_THEN_cr_then_custom_then_each_mods_cr_scenarios_in_launcher_order()
        {
            IReadOnlyList<ScenarioPackFolder> folders = ScenarioPackFolders.List(@"G\CR\ModuleData\scenarios", @"D:\MyCR",
                new[] {("Bellum", @"G\Modules\Bellum"), ("VividWorld", @"G\Modules\VividWorld")}, "CalradiaRemembers");

            folders.Select(f => f.Source).Should().Equal("CalradiaRemembers", "Custom", "Bellum", "VividWorld");
            folders[1].Folder.Should().Be(System.IO.Path.Combine(@"D:\MyCR", "scenarios"));
            folders[3].Folder.Should().Be(System.IO.Path.Combine(@"G\Modules\VividWorld", "ModuleData", "cr_scenarios"));
        }

        // STAKE: CR among the enabled mods must not read its own packs a second time: every shipped rule would be
        // reported as a duplicate of itself.
        [Test]
        public void GIVEN_calradia_remembers_among_the_enabled_mods_WHEN_folders_are_listed_THEN_its_packs_are_read_once()
        {
            ScenarioPackFolders.List(@"G\CR\ModuleData\scenarios", null, new[] {("CalradiaRemembers", @"G\CR"), ("Bellum", @"G\Bellum")}, "CalradiaRemembers")
                               .Select(f => f.Source).Should().Equal("CalradiaRemembers", "Bellum");
        }

        // STAKE: an engine list that repeats a module or carries a blank entry must not read a mod's packs twice (each rule
        // would be reported as a duplicate of itself) nor build a folder out of nothing.
        [Test]
        public void GIVEN_a_repeated_or_blank_module_WHEN_folders_are_listed_THEN_each_mod_is_read_once_and_blanks_are_skipped()
        {
            ScenarioPackFolders.List(null, null, new[] {("Bellum", @"G\Bellum"), ("", @"G\x"), ("Other", null), ("BELLUM", @"G\Bellum")}, "CalradiaRemembers")
                               .Select(f => f.Source).Should().Equal("Bellum");
        }

        // STAKE: a mod is named after its folder name "cr_scenarios", never a bare "scenarios": another mod's own folder
        // of that common name, holding something else entirely, must not be read as packs.
        [Test]
        public void GIVEN_a_mod_WHEN_its_folder_is_listed_THEN_it_is_cr_scenarios()
        {
            ScenarioPackFolders.List(null, null, new[] {("Bellum", @"G\Bellum")}, "CalradiaRemembers")
                               .Single().Folder.Should().EndWith("cr_scenarios");
        }

        // STAKE: seals are kept by rule id alone. If a mod's rule kept an id of CR's, firing either would switch the
        // other off for the rest of the campaign; the first read keeps it and the author is told whose id it is.
        [Test]
        public void GIVEN_two_packs_sharing_a_rule_id_WHEN_merged_THEN_the_first_keeps_it_and_the_later_one_is_reported()
        {
            ScenarioMergeResult r = ScenarioRuleMerge.Merge(new[] {
                new SourcedScenarioRule {Source = "CalradiaRemembers", Rule = Rule("rhagaea_death_eagles_turn")},
                new SourcedScenarioRule {Source = "Bellum", Rule = Rule("Rhagaea_Death_Eagles_Turn")},
                new SourcedScenarioRule {Source = "Bellum", Rule = Rule("bellum_siege_song")}
            });

            r.Rules.Select(x => x.Id).Should().Equal("rhagaea_death_eagles_turn", "bellum_siege_song");
            r.SourceOf["rhagaea_death_eagles_turn"].Should().Be("CalradiaRemembers");
            r.SourceOf["bellum_siege_song"].Should().Be("Bellum");
            r.Problems.Should().ContainSingle().Which.Should().Contain("Bellum").And.Contain("CalradiaRemembers");
        }

        // STAKE: a rule with no id can never be sealed nor told apart; it must not take a place nor break the merge.
        [Test]
        public void GIVEN_a_missing_rule_WHEN_merged_THEN_it_is_skipped()
        {
            ScenarioRuleMerge.Merge(new[] {null, new SourcedScenarioRule {Source = "Custom", Rule = null}, new SourcedScenarioRule {Source = "Custom", Rule = Rule("mine")}})
                             .Rules.Select(x => x.Id).Should().Equal("mine");
        }
    }
}
