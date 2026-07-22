// Code written by Gabriel Mailhot, 21/07/2026.
// Perception layer: pins the four-band classification NPCs will use to notice how the player is dressed.
// The owner was explicit that genuine nudity must be a REACHABLE, DISTINCT state (not merely "no armour
// while still clothed"), and that Rags-vs-Civilian is a quality read on what is worn. This is the pure
// decision PlayerDressResolver (mod) feeds and PromptBuilder's render-worthiness gate (SDK) consumes; get
// the boundary wrong here and either a fully-clothed player gets called naked, or a genuinely bare one
// reads as merely under-dressed.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PlayerDressPolicyTests
    {
        // The one case the owner called out by name: nothing worn anywhere must read as Naked, full stop,
        // regardless of what the tier/armour flags happen to say (they are meaningless with nothing on).
        [Test]
        public void GIVEN_nothing_covers_the_body_or_legs_WHEN_classifying_THEN_the_band_is_Naked()
        {
            PlayerDressPolicy.Classify(isBodyOrLegCovered: false, isWearingBattleArmor: false, garmentTier: 0)
                .Should().Be(PlayerDressBand.Naked);
        }

        // Guards the exact failure mode named in the brief: "must not be satisfied by merely lacking armour
        // while still clothed." A contradictory isWearingBattleArmor=true can never override "nothing worn" -
        // coverage is checked first and is authoritative.
        [Test]
        public void GIVEN_isBodyOrLegCovered_is_false_WHEN_the_armor_flag_contradictorily_says_true_THEN_the_band_is_still_Naked()
        {
            PlayerDressPolicy.Classify(isBodyOrLegCovered: false, isWearingBattleArmor: true, garmentTier: 5)
                .Should().Be(PlayerDressBand.Naked);
        }

        // Real battle protection wins outright once something covers the body/legs: an armoured player is
        // never reclassified down to a garment-tier read, however that tier happens to be set.
        [Test]
        public void GIVEN_battle_armor_is_worn_WHEN_classifying_THEN_the_band_is_Armoured_regardless_of_garment_tier()
        {
            PlayerDressPolicy.Classify(isBodyOrLegCovered: true, isWearingBattleArmor: true, garmentTier: 0)
                .Should().Be(PlayerDressBand.Armoured);
        }

        // Pins the Rags/Civilian boundary exactly at RagsMaxTier: Bannerlord's cheapest civilian tunics sit
        // at Tier1 (int 0), which must read as ragged, not merely "plain clothes", the distinction the
        // owner asked to be a genuine quality read on what is worn.
        [Test]
        public void GIVEN_civilian_clothing_at_the_lowest_tier_WHEN_classifying_THEN_the_band_is_Rags()
        {
            PlayerDressPolicy.Classify(isBodyOrLegCovered: true, isWearingBattleArmor: false, garmentTier: PlayerDressPolicy.RagsMaxTier)
                .Should().Be(PlayerDressBand.Rags);
        }

        // The boundary's other side: one tier above RagsMaxTier is presentable townswear, not rags.
        [Test]
        public void GIVEN_civilian_clothing_one_tier_above_the_rags_ceiling_WHEN_classifying_THEN_the_band_is_Civilian()
        {
            PlayerDressPolicy.Classify(isBodyOrLegCovered: true, isWearingBattleArmor: false, garmentTier: PlayerDressPolicy.RagsMaxTier + 1)
                .Should().Be(PlayerDressBand.Civilian);
        }

        // Defensive: an out-of-range (negative) tier must resolve, not throw - the policy only ever compares
        // primitives, so a malformed upstream fact degrades to a bucket rather than crashing the prompt build.
        [Test]
        public void GIVEN_a_negative_garment_tier_WHEN_classifying_THEN_it_resolves_without_throwing()
        {
            PlayerDressPolicy.Classify(isBodyOrLegCovered: true, isWearingBattleArmor: false, garmentTier: -7)
                .Should().Be(PlayerDressBand.Rags);
        }
    }
}
