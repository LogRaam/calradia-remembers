// Code written by Gabriel Mailhot, 08/07/2026.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestMenuComposerTests
    {
        [Test]
        public void GIVEN_a_grounded_entry_WHEN_composing_THEN_the_line_names_the_type_the_target_field_and_the_real_target()
        {
            var entries = new List<QuestMenuEntry> {
                new("bandit_hideout", "target_settlement", "Diathma", "a bandit lair infests the country nearby")
            };

            QuestMenuComposer.Compose(entries)
                .Should().Be("- bandit_hideout (target_settlement: Diathma): a bandit lair infests the country nearby");
        }

        [Test]
        public void GIVEN_a_targetless_entry_WHEN_composing_THEN_no_target_parenthesis_appears()
        {
            var entries = new List<QuestMenuEntry> {
                new("deliver_items", null, null, "goods in payment, the game sets the value")
            };

            QuestMenuComposer.Compose(entries)
                .Should().Be("- deliver_items: goods in payment, the game sets the value");
        }

        [Test]
        public void GIVEN_no_viable_entries_WHEN_composing_THEN_the_menu_is_empty_so_nothing_is_taught()
        {
            QuestMenuComposer.Compose(new List<QuestMenuEntry>()).Should().Be("");
            QuestMenuComposer.Compose(null).Should().Be("");
        }

        [Test]
        public void GIVEN_more_entries_than_the_cap_WHEN_composing_THEN_the_menu_is_truncated_to_the_cap()
        {
            var entries = new List<QuestMenuEntry>();
            for (var i = 0; i < QuestMenuComposer.MaxEntries + 3; i++)
                entries.Add(new QuestMenuEntry($"type_{i}", null, null, "cue"));

            string menu = QuestMenuComposer.Compose(entries);

            menu.Split('\n').Length.Should().Be(QuestMenuComposer.MaxEntries);
        }

        [Test]
        public void GIVEN_a_null_or_empty_entry_WHEN_composing_THEN_it_is_skipped_without_wasting_a_cap_slot()
        {
            var entries = new List<QuestMenuEntry> {
                null,
                new("", null, null, "no type token"),
                new("attack_lord", "target_hero", "Caladog", "an enemy of your realm rides these lands")
            };

            QuestMenuComposer.Compose(entries)
                .Should().Be("- attack_lord (target_hero: Caladog): an enemy of your realm rides these lands");
        }
    }
}
