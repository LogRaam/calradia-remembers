// Code written by Gabriel Mailhot, 12/07/2026.
// Documents when two witness entries are the same person, so the "Present:" list never shows a duplicate
// (player report: bringing a companion who is already present listed their name twice).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class WitnessDedupTests
    {
        private static WitnessEntry Hero(string id, string name) => new() {HeroStringId = id, Name = name, RelationToNpc = ""};
        private static WitnessEntry Synthetic(string name) => new() {Name = name, RelationToNpc = ""};

        [Test]
        public void GIVEN_two_entries_with_the_same_hero_id_WHEN_compared_THEN_they_are_the_same_even_if_names_differ()
            => WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), Hero("lord_1", "Ladogual the Bold")).Should().BeTrue();

        [Test]
        public void GIVEN_two_entries_with_different_hero_ids_WHEN_compared_THEN_they_are_distinct()
            => WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), Hero("lord_2", "Ladogual")).Should().BeFalse();

        [Test]
        public void GIVEN_a_hero_and_a_same_named_heroless_bystander_WHEN_compared_THEN_they_stay_distinct()
            => WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), Synthetic("Ladogual")).Should().BeFalse();

        [Test]
        public void GIVEN_two_heroless_entries_with_the_same_name_WHEN_compared_THEN_they_are_the_same()
            => WitnessDedup.IsSame(Synthetic("A gang enforcer"), Synthetic("a gang enforcer")).Should().BeTrue();

        [Test]
        public void GIVEN_two_heroless_entries_with_different_names_WHEN_compared_THEN_they_are_distinct()
            => WitnessDedup.IsSame(Synthetic("A gang enforcer"), Synthetic("An apprentice")).Should().BeFalse();

        [Test]
        public void GIVEN_a_null_entry_WHEN_compared_THEN_it_is_never_the_same()
        {
            WitnessDedup.IsSame(null, Hero("lord_1", "Ladogual")).Should().BeFalse();
            WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), null).Should().BeFalse();
        }
    }
}
