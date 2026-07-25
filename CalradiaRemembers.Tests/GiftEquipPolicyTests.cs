// Code written by Gabriel Mailhot, 25/07/2026.
// This is the pure core of the "gift equipment in chat" feature (2026-07-25): when the player gifts an
// equipment item to an NPC, the mod's give_item bridge step reads GiftEquipPolicy.Decide's verdict and, when
// ShouldEquip is true, actually equips the item on the NPC (the engine half, done separately), then narrates
// the reaction either way. If this policy is wrong in one direction, an NPC's gear silently DOWNGRADES from a
// random trinket the player handed over out of roleplay, which reads as a bug the moment the player notices
// their companion is suddenly worse-armed than before. If it is wrong in the other direction, a wife never
// wears the dress the player gifted her, or a companion who would clearly like a sword keeps fighting
// barehanded, which is the whole feature failing to deliver on its promise. Every test below is anchored to
// that real request: gift equipment in chat, let it actually be worn when it makes sense (robe to the wife, a
// sword a companion likes), a rival keeps his own gear, and nobody is silently downgraded.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class GiftEquipPolicyTests
    {
        // STAKE: only real equipment should ever touch a hero's equipment slots. A trade good (grain, cloth,
        // a jewel) gifted in chat must never be "equipped", even into an empty slot, even for a beloved
        // spouse: the ENGINE has no equipment mapping for it, so equipping would be nonsensical or crash.
        [Test]
        public void GIVEN_a_non_equippable_gift_WHEN_deciding_THEN_it_is_never_equipped_even_into_an_empty_slot_for_a_beloved_spouse()
        {
            var facts = new GiftEquipFacts(
                isEquippable: false,
                slotEmpty: true,
                giftValue: 500,
                currentSlotValue: 0,
                relation: 100,
                isSentimental: true);

            var decision = GiftEquipPolicy.Decide(facts);

            decision.ShouldEquip.Should().BeFalse();
            decision.Reason.Should().Be(GiftEquipReason.NotEquippable);
        }

        // STAKE: something is better than nothing. A barehanded companion should take a gifted sword
        // regardless of how the player and that companion currently stand with each other.
        [Test]
        public void GIVEN_an_empty_target_slot_WHEN_deciding_THEN_the_gift_is_always_equipped_regardless_of_relation()
        {
            var facts = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: true,
                giftValue: 10,
                currentSlotValue: 0,
                relation: -50,
                isSentimental: false);

            var decision = GiftEquipPolicy.Decide(facts);

            decision.ShouldEquip.Should().BeTrue();
            decision.Reason.Should().Be(GiftEquipReason.EmptySlot);
        }

        // STAKE: a clearly better item is worn on merit, not affection. Even a rival lord who barely
        // tolerates the player will put on a strictly superior blade over his own worse one.
        [Test]
        public void GIVEN_a_strict_upgrade_WHEN_deciding_THEN_it_is_equipped_even_for_a_low_relation_npc()
        {
            var facts = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 200,
                currentSlotValue: 100,
                relation: -20,
                isSentimental: false);

            var decision = GiftEquipPolicy.Decide(facts);

            decision.ShouldEquip.Should().BeTrue();
            decision.Reason.Should().Be(GiftEquipReason.Upgrade);
        }

        // STAKE: the wife wears the gifted robe though her own gown was finer. Sentimental bonds (spouse, the
        // player's own companion) override cold value math even at low personal regard.
        [Test]
        public void GIVEN_a_non_upgrade_gift_to_a_sentimental_npc_WHEN_deciding_THEN_it_is_worn_out_of_affection_even_at_low_relation()
        {
            var facts = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 50,
                currentSlotValue: 120,
                relation: 5,
                isSentimental: true);

            var decision = GiftEquipPolicy.Decide(facts);

            decision.ShouldEquip.Should().BeTrue();
            decision.Reason.Should().Be(GiftEquipReason.Affection);
        }

        // STAKE: a close friend wears your gift fondly, even without the spouse/companion bond, once regard
        // is high enough.
        [Test]
        public void GIVEN_a_non_upgrade_gift_at_or_above_the_affection_threshold_WHEN_deciding_THEN_it_is_worn_without_being_sentimental()
        {
            var facts = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 50,
                currentSlotValue: 120,
                relation: GiftEquipPolicy.AffectionThreshold,
                isSentimental: false);

            var decision = GiftEquipPolicy.Decide(facts);

            decision.ShouldEquip.Should().BeTrue();
            decision.Reason.Should().Be(GiftEquipReason.Affection);
        }

        // STAKE: a rival lord keeps his own trusted blade; the gift is accepted as a gesture (still handed
        // over, still narrated) but never worn over gear he already trusts more.
        [Test]
        public void GIVEN_a_non_upgrade_gift_to_a_cold_non_sentimental_npc_WHEN_deciding_THEN_their_own_gear_is_kept()
        {
            var facts = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 50,
                currentSlotValue: 120,
                relation: GiftEquipPolicy.AffectionThreshold - 1,
                isSentimental: false);

            var decision = GiftEquipPolicy.Decide(facts);

            decision.ShouldEquip.Should().BeFalse();
            decision.Reason.Should().Be(GiftEquipReason.KeptOwnGear);
        }

        // STAKE: pins the tunable threshold so a later retune of AffectionThreshold is a deliberate, visible
        // change rather than an accidental off-by-one that silently mutes or over-triggers Affection.
        [Test]
        public void GIVEN_relation_exactly_one_below_the_affection_threshold_WHEN_deciding_THEN_it_does_not_equip()
        {
            var facts = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 50,
                currentSlotValue: 120,
                relation: GiftEquipPolicy.AffectionThreshold - 1,
                isSentimental: false);

            GiftEquipPolicy.Decide(facts).ShouldEquip.Should().BeFalse();
        }

        // STAKE: a lateral swap (equal value) is not worn on merit alone, only out of fondness: equal value
        // must fall through to the affection/keep branches rather than being mistaken for an Upgrade.
        [Test]
        public void GIVEN_a_gift_worth_exactly_the_same_as_the_current_item_WHEN_deciding_THEN_it_is_not_treated_as_an_upgrade()
        {
            var equalValueColdNpc = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 100,
                currentSlotValue: 100,
                relation: 0,
                isSentimental: false);

            var equalValueSentimentalNpc = new GiftEquipFacts(
                isEquippable: true,
                slotEmpty: false,
                giftValue: 100,
                currentSlotValue: 100,
                relation: 0,
                isSentimental: true);

            GiftEquipPolicy.Decide(equalValueColdNpc).Reason.Should().Be(GiftEquipReason.KeptOwnGear);
            GiftEquipPolicy.Decide(equalValueSentimentalNpc).Reason.Should().Be(GiftEquipReason.Affection);
        }
    }
}
