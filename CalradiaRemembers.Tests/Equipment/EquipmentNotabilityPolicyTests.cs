// Code written by Gabriel Mailhot, 29/08/2026.
// EquipmentNotabilityPolicy decides which equipped pieces are worth telling the model about and phrases them.
// It feeds a new prompt block (the player's, a present companion's/witness's, and the speaking NPC's own
// notable gear), which is why the exact wording and the "ordinary stays silent" discipline are pinned here:
// a modder's report was that CR feeds NO equipment to the prompt, so an NPC's reaction to a mastercraft blade
// or shabby rags is pure guesswork. If this policy over-voices (lists every common piece) it bloats the lean
// prompt; if it under-voices it never fixes the report. Gabriel reads these tests to confirm both edges.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Items;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Equipment
{
    [TestFixture]
    public sealed class EquipmentNotabilityPolicyTests
    {
        private static EquipmentPieceFacts Piece(
            GearKind kind, string noun, GearQuality quality, int tier = 0, int value = 0)
            => new EquipmentPieceFacts(kind, noun, quality, tier, value);

        // STAKE: the core discipline. An ordinary loadout must produce NO line, or every NPC conversation
        // carries a pointless "He is clad in common armor" and the lean prompt (hard ~6950-char budget) pays
        // for noise. Common in, empty string out.
        [Test]
        public void GIVEN_only_common_pieces_WHEN_composing_THEN_nothing_is_voiced()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Armor, "armor", GearQuality.Common, tier: 4, value: 2000),
                Piece(GearKind.Weapon, "sword", GearQuality.Common, tier: 4, value: 1500)
            };

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false).Should().BeEmpty();
        }

        // STAKE: the headline case the report asks for, a companion's mastercraft blade the NPC should notice.
        // Third-person Subject voice, correct male pronoun, finite verb.
        [Test]
        public void GIVEN_a_masterwork_sword_WHEN_composing_subject_male_THEN_he_bears_it()
        {
            var pieces = new List<EquipmentPieceFacts> {Piece(GearKind.Weapon, "sword", GearQuality.Masterwork)};

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false)
                .Should().Be("He bears a masterwork sword.");
        }

        // STAKE: the Self voice is what closes the gift loop, a legendary sword the player just gave becomes
        // something the NPC can reference in its own words ("You bear a legendary blade"). Second person, and
        // the article must agree ("a legendary", not "an").
        [Test]
        public void GIVEN_a_legendary_weapon_WHEN_composing_self_THEN_you_bear_it()
        {
            var pieces = new List<EquipmentPieceFacts> {Piece(GearKind.Weapon, "blade", GearQuality.Legendary)};

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false, AppearanceVoice.Self)
                .Should().Be("You bear a legendary blade.");
        }

        // STAKE: the "girl in rags" case, in the opposite direction. Conspicuously bad gear must speak up too,
        // and be worded per kind (shabby armor, not "poor armor"), with the female pronoun honored.
        [Test]
        public void GIVEN_poor_armor_WHEN_composing_subject_female_THEN_she_wears_shabby_armor()
        {
            var pieces = new List<EquipmentPieceFacts> {Piece(GearKind.Armor, "armor", GearQuality.Poor)};

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: true)
                .Should().Be("She wears shabby armor.");
        }

        // STAKE: the player's own cited case was a "dress", not plate. A countable garment must take an article
        // ("a fine gown"), where a mass noun like armor does not ("fine armor"). Guards the attire path so a
        // gifted gown never reads as "wears fine gown".
        [Test]
        public void GIVEN_a_fine_gown_WHEN_composing_subject_female_THEN_the_article_is_present()
        {
            var pieces = new List<EquipmentPieceFacts> {Piece(GearKind.Armor, "gown", GearQuality.Fine)};

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: true)
                .Should().Be("She wears a fine gown.");
        }

        // STAKE: modded uniques (a TOR legendary blade) often carry NO vanilla modifier, so quality reads
        // Common; without value-promotion they would be invisible, the exact blind spot the modder hit. A
        // Common piece at/above the value threshold must be voiced as fine.
        [Test]
        public void GIVEN_an_unmarked_but_very_costly_weapon_WHEN_composing_THEN_it_is_voiced_as_fine()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Weapon, "sword", GearQuality.Common,
                    value: EquipmentNotabilityPolicy.NotableValueThreshold)
            };

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false)
                .Should().Be("He bears a fine sword.");
        }

        // STAKE: a piece just below the threshold with no modifier stays silent, so the promotion in the test
        // above is a real boundary and not "any weapon is fine". Guards the threshold from drifting to zero.
        [Test]
        public void GIVEN_an_unmarked_weapon_below_the_value_threshold_WHEN_composing_THEN_nothing_is_voiced()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Weapon, "sword", GearQuality.Common,
                    value: EquipmentNotabilityPolicy.NotableValueThreshold - 1)
            };

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false).Should().BeEmpty();
        }

        // STAKE: a full plate harness is five armor slots; without best-per-kind folding the line would read
        // "clad in fine armor and clad in fine armor". One armor clause only, and it must be the BEST slot.
        [Test]
        public void GIVEN_several_armor_pieces_WHEN_composing_THEN_only_the_best_single_armor_clause_appears()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Armor, "armor", GearQuality.Fine, value: 8000),
                Piece(GearKind.Armor, "armor", GearQuality.Masterwork, value: 12000),
                Piece(GearKind.Armor, "armor", GearQuality.Fine, value: 3000)
            };

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false)
                .Should().Be("He wears masterwork armor.");
        }

        // STAKE: worn-then-carried reading order, and the two-piece cap. A hero with fine armor and a
        // masterwork weapon reads as one sentence, armor first, joined by "and", never a third clause.
        [Test]
        public void GIVEN_notable_armor_and_weapon_WHEN_composing_THEN_armor_leads_then_weapon_joined_by_and()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Weapon, "axe", GearQuality.Masterwork),
                Piece(GearKind.Armor, "armor", GearQuality.Fine)
            };

            EquipmentNotabilityPolicy.Compose(pieces, isFemale: false)
                .Should().Be("He wears fine armor and bears a masterwork axe.");
        }

        // STAKE: the cap must keep the MOST notable pieces, not the first two seen. Given three notable kinds,
        // the legendary weapon and the masterwork armor must survive and the merely-fine horse must be dropped.
        [Test]
        public void GIVEN_three_notable_kinds_WHEN_composing_THEN_the_two_most_notable_survive_the_cap()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Mount, "warhorse", GearQuality.Fine, value: 5000),
                Piece(GearKind.Weapon, "blade", GearQuality.Legendary, value: 30000),
                Piece(GearKind.Armor, "armor", GearQuality.Masterwork, value: 12000)
            };

            string result = EquipmentNotabilityPolicy.Compose(pieces, isFemale: false);

            result.Should().Be("He wears masterwork armor and bears a legendary blade.");
            result.Should().NotContain("warhorse");
        }

        // STAKE: a null or empty loadout must degrade to "", never throw, since the host calls this for every
        // hero in a scene (player, companions, witnesses, the NPC) on every turn.
        [Test]
        public void GIVEN_null_or_empty_pieces_WHEN_composing_THEN_it_is_empty_and_does_not_throw()
        {
            EquipmentNotabilityPolicy.Compose(null, isFemale: false).Should().BeEmpty();
            EquipmentNotabilityPolicy.Compose(new List<EquipmentPieceFacts>(), isFemale: true).Should().BeEmpty();
        }

        // ── ComposeWorn (full gear, chosen 2026-09-02 to end the model's guessing) ──────────────────────────

        // THE PLAYER REPORT (fkasad/TOR): ordinary gear left the model guessing (a lady read as in chainmail, an
        // archer as bearing a shield and pick). ComposeWorn states the plain fact instead: ORDINARY pieces are
        // named with no quality adjective, so nothing is invented and nothing is silently dropped.
        [Test]
        public void GIVEN_ordinary_armor_and_weapon_WHEN_composing_worn_THEN_it_states_the_plain_gear()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Armor, "mail", GearQuality.Common),
                Piece(GearKind.Weapon, "bow", GearQuality.Common)
            };

            EquipmentNotabilityPolicy.ComposeWorn(pieces, isFemale: false)
                .Should().Be("He wears mail and bears a bow.");
        }

        // STAKE: a notable piece keeps its quality adjective even in the full-gear clause, so a legendary blade
        // still reads as legendary, not flattened to "a sword". Quality survives the switch to full gear.
        [Test]
        public void GIVEN_a_mix_of_ordinary_and_notable_WHEN_composing_worn_THEN_only_the_notable_piece_is_adjectived()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Armor, "mail", GearQuality.Common),
                Piece(GearKind.Weapon, "sword", GearQuality.Legendary)
            };

            EquipmentNotabilityPolicy.ComposeWorn(pieces, isFemale: false)
                .Should().Be("He wears mail and bears a legendary sword.");
        }

        // STAKE: all three kinds present read in order (worn, carried, ridden) with a natural comma-and join, so
        // the scene has the full picture in one terse clause. The civilian-loadout split upstream keeps the horse
        // out of a tavern scene; here we just pin the phrasing when a mount is in the set.
        [Test]
        public void GIVEN_armor_weapon_and_mount_WHEN_composing_worn_THEN_all_three_read_in_order()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Mount, "horse", GearQuality.Common),
                Piece(GearKind.Weapon, "sword", GearQuality.Common),
                Piece(GearKind.Armor, "mail", GearQuality.Common)
            };

            EquipmentNotabilityPolicy.ComposeWorn(pieces, isFemale: false)
                .Should().Be("He wears mail, bears a sword, and rides a horse.");
        }

        // STAKE: the Self voice, second person, so the speaking NPC can reference their own kit ("You wear...").
        [Test]
        public void GIVEN_gear_WHEN_composing_worn_in_self_voice_THEN_it_is_second_person()
        {
            var pieces = new List<EquipmentPieceFacts> {Piece(GearKind.Armor, "attire", GearQuality.Fine)};

            EquipmentNotabilityPolicy.ComposeWorn(pieces, isFemale: true, AppearanceVoice.Self)
                .Should().Be("You wear fine attire.");
        }

        // STAKE: fkasad's exact confabulation, an archer read as "shield and pick". With two weapons in the set,
        // best-per-kind names ONE (the more valuable), so the clause states a single true weapon, not a pile.
        [Test]
        public void GIVEN_two_weapons_WHEN_composing_worn_THEN_the_more_valuable_one_is_named()
        {
            var pieces = new List<EquipmentPieceFacts>
            {
                Piece(GearKind.Weapon, "shield", GearQuality.Common, value: 200),
                Piece(GearKind.Weapon, "bow", GearQuality.Common, value: 900)
            };

            EquipmentNotabilityPolicy.ComposeWorn(pieces, isFemale: false)
                .Should().Be("He bears a bow.");
        }

        // STAKE: same null/empty safety as Compose, since the host calls this per hero per turn.
        [Test]
        public void GIVEN_null_or_empty_pieces_WHEN_composing_worn_THEN_it_is_empty_and_does_not_throw()
        {
            EquipmentNotabilityPolicy.ComposeWorn(null, isFemale: false).Should().BeEmpty();
            EquipmentNotabilityPolicy.ComposeWorn(new List<EquipmentPieceFacts>(), isFemale: true).Should().BeEmpty();
        }
    }
}
