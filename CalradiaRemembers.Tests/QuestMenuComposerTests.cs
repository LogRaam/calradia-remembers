// Code written by Gabriel Mailhot, 08/07/2026.
// The prompt-facing text for the viable-quest referential (ratified 2026-07-08): the fix for "the NPC offered
// a task but no quest was registered" is to stop teaching the LLM 17 quest types in the abstract and instead
// teach ONLY what a giver can truly hand out right now, real targets named. This formatter is the last mile of
// that promise: get the wording wrong (a missing target, a bad line count) and the LLM either invents a
// target the factory cannot ground, or the prompt bloats past its budget. MaxEntries caps the line count so a
// giver with many viable tasks never blows out the prompt.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestMenuComposerTests
    {
        // This exact wording is the contract the LLM must echo back verbatim (type token + real target name)
        // for QuestFactory to resolve the quest; drifting the format here silently breaks that contract.
        [Test]
        public void GIVEN_a_grounded_entry_WHEN_composing_THEN_the_line_names_the_type_the_target_field_and_the_real_target()
        {
            var entries = new List<QuestMenuEntry> {
                new() {TypeToken = "bandit_hideout", TargetField = "target_settlement", TargetName = "Diathma", Cue = "a bandit lair infests the country nearby"}
            };

            QuestMenuComposer.Compose(entries)
                .Should().Be("- bandit_hideout (target_settlement: Diathma): a bandit lair infests the country nearby");
        }

        // Some quest types (deliver_items) name no target at all; printing an empty or malformed
        // parenthesis here would falsely teach the LLM a target field it has nothing real to fill in.
        [Test]
        public void GIVEN_a_targetless_entry_WHEN_composing_THEN_no_target_parenthesis_appears()
        {
            var entries = new List<QuestMenuEntry> {
                new() {TypeToken = "deliver_items", TargetField = null, TargetName = null, Cue = "goods in payment, the game sets the value"}
            };

            QuestMenuComposer.Compose(entries)
                .Should().Be("- deliver_items: goods in payment, the game sets the value");
        }

        // A giver with nothing viable to offer must teach nothing, not an empty bullet or a crash; "" is
        // the signal the prompt builder uses to fall back to no quest menu at all.
        [Test]
        public void GIVEN_no_viable_entries_WHEN_composing_THEN_the_menu_is_empty_so_nothing_is_taught()
        {
            QuestMenuComposer.Compose(new List<QuestMenuEntry>()).Should().Be("");
            QuestMenuComposer.Compose(null).Should().Be("");
        }

        // MaxEntries is the hard cap protecting the prompt budget; a giver who could truly offer more must
        // still never bloat the prompt past this ceiling.
        [Test]
        public void GIVEN_more_entries_than_the_cap_WHEN_composing_THEN_the_menu_is_truncated_to_the_cap()
        {
            var entries = new List<QuestMenuEntry>();
            for (var i = 0; i < QuestMenuComposer.MaxEntries + 3; i++)
                entries.Add(new QuestMenuEntry {TypeToken = $"type_{i}", TargetField = null, TargetName = null, Cue = "cue"});

            string menu = QuestMenuComposer.Compose(entries);

            menu.Split('\n').Length.Should().Be(QuestMenuComposer.MaxEntries);
        }

        // A null or type-less entry must be skipped for free: charging it a cap slot would silently push a
        // real, viable task off the menu (the world scan's defensive output must never crowd out genuine offers).
        [Test]
        public void GIVEN_a_null_or_empty_entry_WHEN_composing_THEN_it_is_skipped_without_wasting_a_cap_slot()
        {
            var entries = new List<QuestMenuEntry> {
                null!,
                new() {TypeToken = "", TargetField = null, TargetName = null, Cue = "no type token"},
                new() {TypeToken = "attack_lord", TargetField = "target_hero", TargetName = "Caladog", Cue = "an enemy of your realm rides these lands"}
            };

            QuestMenuComposer.Compose(entries)
                .Should().Be("- attack_lord (target_hero: Caladog): an enemy of your realm rides these lands");
        }
    }
}
