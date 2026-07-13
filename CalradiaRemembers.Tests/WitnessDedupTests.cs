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

        // The reported bug itself: the auto-included entry and the "Bring"-added entry for the same lord can
        // carry differently-decorated display names, so matching on name alone would have missed the duplicate.
        // StringId is the ground truth for a real hero; the name is cosmetic.
        [Test]
        public void GIVEN_two_entries_with_the_same_hero_id_WHEN_compared_THEN_they_are_the_same_even_if_names_differ()
            => WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), Hero("lord_1", "Ladogual the Bold")).Should().BeTrue();

        // Two different heroes can share or nearly share a name; collapsing them on name alone would silently
        // drop a real, distinct witness from the Present list.
        [Test]
        public void GIVEN_two_entries_with_different_hero_ids_WHEN_compared_THEN_they_are_distinct()
            => WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), Hero("lord_2", "Ladogual")).Should().BeFalse();

        // A synthetic flavor witness (no HeroStringId) can coincidentally share a name with a real hero; the
        // class header calls this out explicitly, so a hero must never be swallowed by a same-named bystander.
        [Test]
        public void GIVEN_a_hero_and_a_same_named_heroless_bystander_WHEN_compared_THEN_they_stay_distinct()
            => WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), Synthetic("Ladogual")).Should().BeFalse();

        // Heroless witnesses carry no id at all, so name is the only signal; the auto-included path and a
        // manual "Bring" could add the same flavor witness with differing case, and that must still dedupe.
        [Test]
        public void GIVEN_two_heroless_entries_with_the_same_name_WHEN_compared_THEN_they_are_the_same()
            => WitnessDedup.IsSame(Synthetic("A gang enforcer"), Synthetic("a gang enforcer")).Should().BeTrue();

        // Guards the other side of the name-only match: two genuinely different synthetic witnesses must not
        // be over-collapsed into one.
        [Test]
        public void GIVEN_two_heroless_entries_with_different_names_WHEN_compared_THEN_they_are_distinct()
            => WitnessDedup.IsSame(Synthetic("A gang enforcer"), Synthetic("An apprentice")).Should().BeFalse();

        // Fail safe: a missing entry on either side must never be read as a match, or a null slipping into the
        // witness list could silently absorb (hide) a real witness instead of being treated as no witness at all.
        [Test]
        public void GIVEN_a_null_entry_WHEN_compared_THEN_it_is_never_the_same()
        {
            WitnessDedup.IsSame(null, Hero("lord_1", "Ladogual")).Should().BeFalse();
            WitnessDedup.IsSame(Hero("lord_1", "Ladogual"), null).Should().BeFalse();
        }
    }
}
