// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "a blacksmithing quest". Gabriel: the kind of weapon is part of the deal. A lord
// asks the player to forge him a sword, an axe, a spear; only a weapon the PLAYER forged, of that kind, completes it.
// If these rules are wrong, a sword bought at the market passes as the player's work, an axe answers a request for a
// sword, or a lord asks for something no smithy makes and the task can never complete.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CraftWeaponPolicyTests
    {
        // ── What a lord may ask ───────────────────────────────────────────────────────────────────

        // The words a lord reaches for land on the smithy's own families.
        [TestCase("sword", "sword")]
        [TestCase("Greatsword", "two_handed_sword")]
        [TestCase("two-handed sword", "two_handed_sword")]
        [TestCase("battle axe", "two_handed_axe")]
        [TestCase("spear", "polearm")]
        [TestCase("warhammer", "two_handed_mace")]
        [TestCase("throwing knives", "throwing_knife")]
        public void GIVEN_a_lords_word_for_a_weapon_WHEN_normalized_THEN_it_is_a_kind_the_smithy_makes(string word, string kind)
        {
            CraftWeaponPolicy.Normalize(word).Should().Be(kind);
        }

        // A bow, a shield or a crossbow is not forged at the smithy: no such task exists.
        [TestCase("bow")]
        [TestCase("shield")]
        [TestCase("crossbow")]
        [TestCase("")]
        public void GIVEN_something_the_smithy_does_not_make_WHEN_normalized_THEN_there_is_no_kind(string word)
        {
            CraftWeaponPolicy.Normalize(word).Should().BeNull();
        }

        // ── What answers it ───────────────────────────────────────────────────────────────────────

        // A sword the player forged answers a request for a sword; every polearm grip is a polearm.
        [Test]
        public void GIVEN_a_weapon_the_player_forged_of_the_kind_asked_WHEN_handed_over_THEN_it_answers_the_task()
        {
            CraftWeaponPolicy.Answers(true, "OneHandedSword", CraftWeaponPolicy.Key("sword")).Should().BeTrue();
            CraftWeaponPolicy.Answers(true, "LowGripPolearm", CraftWeaponPolicy.Key("polearm")).Should().BeTrue();
        }

        // A bought sword is not the player's work; a forged axe is not the sword that was asked.
        [Test]
        public void GIVEN_a_bought_weapon_or_the_wrong_kind_WHEN_handed_over_THEN_it_does_not_answer()
        {
            CraftWeaponPolicy.Answers(false, "OneHandedSword", CraftWeaponPolicy.Key("sword")).Should().BeFalse();
            CraftWeaponPolicy.Answers(true, "OneHandedAxe", CraftWeaponPolicy.Key("sword")).Should().BeFalse();
            CraftWeaponPolicy.Answers(true, "OneHandedSword", "grain").Should().BeFalse();
        }

        // The forge key never reads as another supply, and reads back its kind.
        [Test]
        public void GIVEN_a_forge_key_WHEN_read_back_THEN_it_gives_its_kind_and_no_other_key_does()
        {
            CraftWeaponPolicy.KindOfKey(CraftWeaponPolicy.Key("two_handed_axe")).Should().Be("two_handed_axe");
            CraftWeaponPolicy.KindOfKey("market:iron").Should().BeNull();
            CraftWeaponPolicy.Label("two_handed_axe").Should().Be("two-handed axe");
        }
    }
}
