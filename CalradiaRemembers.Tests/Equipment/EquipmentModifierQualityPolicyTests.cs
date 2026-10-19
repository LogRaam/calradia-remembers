// Code written by Gabriel Mailhot, 29/08/2026.
// EquipmentModifierQualityPolicy is the ONE place a raw ItemModifier's facts turn into a GearQuality band, so
// its correctness decides whether a companion's masterwork blade or a rusty relic ever gets voiced at all
// (EquipmentNotabilityPolicy stays silent on anything that reads Common). The native-quality-name path must
// win for every vanilla modifier (it is the game's own authoritative band); the price-multiplier fallback
// exists only for a modded ItemModifier with no legible quality name, and its thresholds are pinned to the
// REAL vanilla price_factor values found in the decompiled Modules/Native/ModuleData/item_modifiers.xml
// (legendary=1.8, masterwork=1.5, fine=1.2, inferior=0.6, poor=0.3), so Gabriel can check them against that
// file directly.

#region

using CalradiaRemembers.Logic.Items;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Equipment
{
    [TestFixture]
    public sealed class EquipmentModifierQualityPolicyTests
    {
        // STAKE: the primary path. Every vanilla legendary modifier ("legendary_sword", "legendary_plate"...)
        // carries quality="legendary" in item_modifiers.xml; if the name path did not win outright a vanilla
        // legendary piece could still be misread from price alone.
        [Test]
        public void GIVEN_native_quality_legendary_WHEN_mapping_THEN_it_is_legendary_regardless_of_price()
        {
            EquipmentModifierQualityPolicy.FromModifier("Legendary", priceMultiplier: 1.0)
                .Should().Be(GearQuality.Legendary);
        }

        // STAKE: the mirror case for the game's OTHER positive bands, both read by name alone.
        [Test]
        public void GIVEN_native_quality_masterwork_or_fine_WHEN_mapping_THEN_each_reads_its_own_band()
        {
            EquipmentModifierQualityPolicy.FromModifier("Masterwork", 1.0).Should().Be(GearQuality.Masterwork);
            EquipmentModifierQualityPolicy.FromModifier("Fine", 1.0).Should().Be(GearQuality.Fine);
        }

        // STAKE: TaleWorlds' ItemModifier.ItemQuality enum has SIX bands (Poor/Inferior/Common/Fine/Masterwork/
        // Legendary) but our GearQuality has only ONE negative band. Both "poor" (rusty, 0.3x) and "inferior"
        // (dull, 0.6x) native modifiers must fold into GearQuality.Poor, or the game's own middle band (a real,
        // shipped vanilla modifier) would silently vanish as unrecognized and fall through to a price guess.
        [Test]
        public void GIVEN_native_quality_poor_or_inferior_WHEN_mapping_THEN_both_fold_into_poor()
        {
            EquipmentModifierQualityPolicy.FromModifier("Poor", 1.0).Should().Be(GearQuality.Poor);
            EquipmentModifierQualityPolicy.FromModifier("Inferior", 1.0).Should().Be(GearQuality.Poor);
        }

        // STAKE: an ordinary, unmodified item must never be voiced (EquipmentNotabilityPolicy's whole
        // discipline). The native "Common" name must map cleanly, not fall through to the keyword or price path.
        [Test]
        public void GIVEN_native_quality_common_WHEN_mapping_THEN_it_is_common()
        {
            EquipmentModifierQualityPolicy.FromModifier("Common", 1.0).Should().Be(GearQuality.Common);
        }

        // STAKE: the price-fallback thresholds are pinned to the VERIFIED vanilla price_factor values (see file
        // header), not invented numbers. This locks the exact figures a modded ItemModifier with no legible
        // quality name is judged against, so a drift here is a deliberate, reviewable change, not an accident.
        [Test]
        public void GIVEN_no_recognizable_name_WHEN_mapping_by_price_THEN_each_verified_vanilla_threshold_lands_correctly()
        {
            EquipmentModifierQualityPolicy.FromModifier("", 1.8).Should().Be(GearQuality.Legendary);
            EquipmentModifierQualityPolicy.FromModifier("", 1.5).Should().Be(GearQuality.Masterwork);
            EquipmentModifierQualityPolicy.FromModifier("", 1.2).Should().Be(GearQuality.Fine);
            EquipmentModifierQualityPolicy.FromModifier("", 0.6).Should().Be(GearQuality.Poor);
            EquipmentModifierQualityPolicy.FromModifier("", 0.3).Should().Be(GearQuality.Poor);
        }

        // STAKE: an unmodified item (no ItemModifier at all) prices at exactly 1.0x. The fallback must read
        // that as Common, not drift a plain item into Fine or Poor.
        [Test]
        public void GIVEN_no_recognizable_name_WHEN_price_is_unmodified_THEN_it_is_common()
        {
            EquipmentModifierQualityPolicy.FromModifier(null, 1.0).Should().Be(GearQuality.Common);
        }

        // STAKE: the keyword fallback exists for a modded ItemModifier whose own quality name is missing or
        // unrecognized but whose descriptive name still says "Rusty" or "Masterwork" plainly. It must win over
        // an inconclusive (near-1.0) price rather than silently reading Common.
        [Test]
        public void GIVEN_unrecognized_name_with_a_known_keyword_WHEN_price_is_inconclusive_THEN_the_keyword_wins()
        {
            EquipmentModifierQualityPolicy.FromModifier("Rusty Sword of the North", priceMultiplier: 1.0)
                .Should().Be(GearQuality.Poor);
            EquipmentModifierQualityPolicy.FromModifier("Masterwork Blade of the North", priceMultiplier: 1.0)
                .Should().Be(GearQuality.Masterwork);
        }

        // STAKE: null/blank inputs (an unmarked item, or a host that resolved nothing) must degrade to a plain
        // price read, never throw, since the host calls this for every equipped slot of every hero every turn.
        [Test]
        public void GIVEN_null_or_blank_name_WHEN_mapping_THEN_it_falls_through_to_price_without_throwing()
        {
            EquipmentModifierQualityPolicy.FromModifier(null, 2.0).Should().Be(GearQuality.Legendary);
            EquipmentModifierQualityPolicy.FromModifier("   ", 0.2).Should().Be(GearQuality.Poor);
        }
    }
}
