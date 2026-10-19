// Code written by Gabriel Mailhot, 31/07/2026.
// This policy decides the save-type BASE ID of the CR quest definer. It exists because Europe1100 / EOE ships a
// definer at the same base(300000000)+id 1 as ours, so both claim save-id 300000001 and the second mod to load
// hard-crashes the game at boot. The rule: keep the shipped 300M base when no collider is present (so existing
// CR-quest saves are byte-for-byte untouched, zero quest loss), and only shift to a distinct base when a
// collider is loaded (so CR and EOE coexist). What breaks if this is wrong: pick the wrong base with no collider
// and every existing CR save loses its in-progress quests; fail to detect the collider and the EOE boot crash
// returns.

#region

using CalradiaRemembers.Logic.Quests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class QuestSaveBasePolicyTests
    {
        // STAKE: with NO collider loaded (the overwhelming majority of players), the base MUST stay the shipped
        // 300M value, because that number is baked into every existing save that holds a CR quest. Any other
        // value here would silently drop those players' in-progress quests on update.
        [Test]
        public void GIVEN_no_collider_WHEN_choosing_the_base_THEN_it_is_the_shipped_300M()
        {
            QuestSaveBasePolicy.Choose(colliderPresent: false).Should().Be(300_000_000);
            QuestSaveBasePolicy.Choose(false).Should().Be(QuestSaveBasePolicy.DefaultBase);
        }

        // STAKE: when a collider (Europe1100) IS loaded, the base MUST shift off 300M, or the boot crash from the
        // shared save-id 300000001 returns for anyone running both mods.
        [Test]
        public void GIVEN_a_collider_WHEN_choosing_the_base_THEN_it_shifts_to_the_coexistence_base()
        {
            QuestSaveBasePolicy.Choose(colliderPresent: true).Should().Be(QuestSaveBasePolicy.CoexistenceBase);
            QuestSaveBasePolicy.CoexistenceBase.Should().NotBe(300_000_000);
        }

        // STAKE: the coexistence base plus our three offsets (ids 1/2/3) must stay a valid positive int, or the
        // definer would register a nonsense/overflowed save-id when EOE is present.
        [Test]
        public void GIVEN_the_coexistence_base_WHEN_offsetting_our_three_ids_THEN_they_stay_valid_positive_ints()
        {
            (QuestSaveBasePolicy.CoexistenceBase + 3).Should().BePositive();
            (QuestSaveBasePolicy.CoexistenceBase + 3).Should().BeLessThan(int.MaxValue);
        }

        // STAKE: detection is by assembly name, case-insensitive. Europe1100 ships its definer in WhileThyCome, so
        // BOTH names must be recognised; a miss here means the crash returns. It must not fire on an unrelated mod.
        [TestCase("WhileThyCome", true)]
        [TestCase("whilethycome", true)]
        [TestCase("Europe1100", true)]
        [TestCase("EUROPE1100", true)]
        [TestCase("SomeOtherMod", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void GIVEN_an_assembly_name_WHEN_asking_if_it_is_a_known_collider_THEN_only_the_EOE_ones_match(string name, bool expected)
        {
            QuestSaveBasePolicy.IsColliderAssembly(name).Should().Be(expected);
        }
    }
}
