// Code written by Gabriel Mailhot, 21/07/2026.
// Captive-escape gear consequence: pins the "most advantageous piece per slot" rule Gabriel ratified as
// DETERMINISTIC (tier, then value, then a fixed ordinal key) and the additive-across-escapes accumulation.
// The hard constraint behind every test below is the brief's explicit "no scenario where gear is lost
// forever": SelectWorn + SelectUnwornRemainder together must always account for the WHOLE pool, so a piece
// can be worn or sit in inventory, but never simply vanish.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Captivity;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CaptiveGearStockPolicyTests
    {
        // ── SelectWorn: tier / value / determinism ───────────────────────────

        // The core of "most advantageous": per the brief, tier is the PRIMARY sort key. Get this wrong and
        // a nemesis fights in rags while a masterwork blade sits untouched in his "inventory", the whole
        // point of accumulating gear across escapes being that he visibly wears your best kit.
        [Test]
        public void GIVEN_two_items_in_the_same_slot_with_different_tiers_WHEN_selecting_worn_THEN_the_higher_tier_wins()
        {
            HeldItem low = Make("sword_low", HeldEquipmentSlot.Weapon0, tier: 2, value: 500);
            HeldItem high = Make("sword_high", HeldEquipmentSlot.Weapon0, tier: 5, value: 100);
            var pool = new List<HeldItem> {low, high};

            IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> worn = CaptiveGearStockPolicy.SelectWorn(pool);

            worn[HeldEquipmentSlot.Weapon0].Should().BeSameAs(high);
        }

        // Value is the documented tie-break UNDER equal tier ("tie-broken by value" in the brief): without
        // it, two same-tier items would resolve on pool order alone, which is not a real rule at all.
        [Test]
        public void GIVEN_two_items_of_equal_tier_WHEN_selecting_worn_THEN_the_higher_value_wins()
        {
            HeldItem cheap = Make("armor_cheap", HeldEquipmentSlot.Body, tier: 4, value: 200);
            HeldItem pricey = Make("armor_pricey", HeldEquipmentSlot.Body, tier: 4, value: 900);
            var pool = new List<HeldItem> {cheap, pricey};

            CaptiveGearStockPolicy.SelectWorn(pool)[HeldEquipmentSlot.Body].Should().BeSameAs(pricey);
        }

        // Gabriel's brief demanded a DETERMINISTIC rule specifically so the outcome never depends on
        // collection order (the same failure mode NemesisRosterPolicy's tie-break test already guards
        // against). Two exact ties, tried both ways round, must land on the same winner every time.
        [Test]
        public void GIVEN_two_items_tied_on_tier_and_value_WHEN_selecting_worn_THEN_the_pick_is_stable_regardless_of_pool_order()
        {
            HeldItem a = Make("cape_aaa", HeldEquipmentSlot.Cape, tier: 3, value: 300);
            HeldItem b = Make("cape_bbb", HeldEquipmentSlot.Cape, tier: 3, value: 300);

            HeldItem winnerForward = CaptiveGearStockPolicy.SelectWorn(new List<HeldItem> {a, b})[HeldEquipmentSlot.Cape];
            HeldItem winnerReversed = CaptiveGearStockPolicy.SelectWorn(new List<HeldItem> {b, a})[HeldEquipmentSlot.Cape];

            winnerForward.Should().BeSameAs(winnerReversed);
            winnerForward.ItemStringId.Should().Be("cape_aaa"); // lower ItemStringId wins the ordinal tie-break, by construction
        }

        // The brief explicitly calls out "handle the mount slot": Horse and HorseHarness must resolve as
        // two INDEPENDENT slots, not collapse into one comparison (which would silently drop a harness, or
        // let a saddle's tier outrank an unrelated horse).
        [Test]
        public void GIVEN_items_across_a_weapon_slot_and_the_mount_slots_WHEN_selecting_worn_THEN_each_slot_resolves_independently()
        {
            HeldItem sword = Make("sword_a", HeldEquipmentSlot.Weapon0, tier: 3, value: 300);
            HeldItem horse = Make("horse_a", HeldEquipmentSlot.Horse, tier: 5, value: 1000);
            HeldItem harness = Make("harness_a", HeldEquipmentSlot.HorseHarness, tier: 2, value: 150);
            var pool = new List<HeldItem> {sword, horse, harness};

            IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> worn = CaptiveGearStockPolicy.SelectWorn(pool);

            worn.Should().HaveCount(3);
            worn[HeldEquipmentSlot.Weapon0].Should().BeSameAs(sword);
            worn[HeldEquipmentSlot.Horse].Should().BeSameAs(horse);
            worn[HeldEquipmentSlot.HorseHarness].Should().BeSameAs(harness);
        }

        // An empty or absent pool (a holder never yet handed any gear) must resolve to nothing worn, not throw.
        [Test]
        public void GIVEN_an_empty_or_null_pool_WHEN_selecting_worn_THEN_an_empty_result_is_returned_without_throwing()
        {
            CaptiveGearStockPolicy.SelectWorn(new List<HeldItem>()).Should().BeEmpty();
            CaptiveGearStockPolicy.SelectWorn(null).Should().BeEmpty();
        }

        // ── SelectUnwornRemainder: the "never lost forever" invariant ────────

        // Pins the brief's explicit guarantee: the losing duplicate of a contested slot does not disappear
        // when its rival is chosen to be worn, it must still be sitting in the remainder.
        [Test]
        public void GIVEN_one_item_beats_another_in_the_same_slot_WHEN_selecting_the_remainder_THEN_the_loser_is_still_present()
        {
            HeldItem worn = Make("sword_worn", HeldEquipmentSlot.Weapon0, tier: 5, value: 100);
            HeldItem benched = Make("sword_benched", HeldEquipmentSlot.Weapon0, tier: 1, value: 50);
            var pool = new List<HeldItem> {worn, benched};

            CaptiveGearStockPolicy.SelectUnwornRemainder(pool).Should().ContainSingle().Which.Should().BeSameAs(benched);
        }

        // The exact "escaped twice with the same sword" case the brief anticipates ("a holder can
        // accumulate several ... over time"): two field-identical duplicates must resolve to ONE worn and
        // ONE still in inventory, never both worn (impossible: one slot) and never both silently merged away.
        [Test]
        public void GIVEN_two_field_identical_duplicate_items_WHEN_resolving_worn_and_remainder_THEN_exactly_one_of_each_is_kept()
        {
            HeldItem first = Make("sword_dup", HeldEquipmentSlot.Weapon1, tier: 4, value: 400);
            HeldItem second = Make("sword_dup", HeldEquipmentSlot.Weapon1, tier: 4, value: 400); // a separate instance, same values
            var pool = new List<HeldItem> {first, second};

            IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> worn = CaptiveGearStockPolicy.SelectWorn(pool);
            IReadOnlyList<HeldItem> remainder = CaptiveGearStockPolicy.SelectUnwornRemainder(pool);

            worn.Should().HaveCount(1);
            remainder.Should().ContainSingle();
            worn[HeldEquipmentSlot.Weapon1].Should().NotBeSameAs(remainder[0]); // one worn, the OTHER instance benched
        }

        // The crux invariant behind "no scenario where gear is lost forever": across every slot, worn +
        // remainder must reconstruct the exact original pool, item for item.
        [Test]
        public void GIVEN_a_mixed_pool_across_several_slots_WHEN_summing_worn_and_remainder_THEN_every_original_item_is_accounted_for()
        {
            var pool = new List<HeldItem> {
                Make("sword_a", HeldEquipmentSlot.Weapon0, tier: 5, value: 100),
                Make("sword_b", HeldEquipmentSlot.Weapon0, tier: 2, value: 900),
                Make("armor_a", HeldEquipmentSlot.Body, tier: 3, value: 300),
                Make("horse_a", HeldEquipmentSlot.Horse, tier: 4, value: 400)
            };

            IReadOnlyDictionary<HeldEquipmentSlot, HeldItem> worn = CaptiveGearStockPolicy.SelectWorn(pool);
            IReadOnlyList<HeldItem> remainder = CaptiveGearStockPolicy.SelectUnwornRemainder(pool);

            (worn.Count + remainder.Count).Should().Be(pool.Count);
        }

        // ── Merge: additive accumulation across escapes ──────────────────────

        // "The player may escape more than once ... accumulation must be additive across escapes": a
        // second haul must ADD to the first, never replace it (or a holder's stock would reset to whatever
        // was taken most recently, losing everything from earlier escapes).
        [Test]
        public void GIVEN_a_holders_existing_stock_WHEN_merging_a_fresh_haul_THEN_both_hauls_are_present_afterward()
        {
            var existing = new List<HeldItem> {Make("sword_old", HeldEquipmentSlot.Weapon0, tier: 2, value: 200)};
            var incoming = new List<HeldItem> {Make("armor_new", HeldEquipmentSlot.Body, tier: 3, value: 300)};

            IReadOnlyList<HeldItem> merged = CaptiveGearStockPolicy.Merge(existing, incoming);

            merged.Should().HaveCount(2);
            merged.Should().Contain(existing[0]);
            merged.Should().Contain(incoming[0]);
        }

        // A holder's FIRST-ever escape has no prior stock; a null existing list (fresh ledger entry) must
        // degrade to "just the new haul," not throw and abort the whole transfer.
        [Test]
        public void GIVEN_a_null_existing_stock_WHEN_merging_a_first_haul_THEN_the_haul_alone_is_returned_without_throwing()
        {
            var incoming = new List<HeldItem> {Make("sword_first", HeldEquipmentSlot.Weapon0, tier: 1, value: 50)};

            CaptiveGearStockPolicy.Merge(null, incoming).Should().ContainSingle().Which.Should().BeSameAs(incoming[0]);
        }

        // A defensive/no-op call (nothing new taken) must not fabricate items or throw.
        [Test]
        public void GIVEN_both_lists_are_null_WHEN_merging_THEN_an_empty_list_is_returned_without_throwing()
        {
            CaptiveGearStockPolicy.Merge(null, null).Should().BeEmpty();
        }

        private static HeldItem Make(string itemStringId, HeldEquipmentSlot slot, int tier, int value)
            => new HeldItem {ItemStringId = itemStringId, Slot = slot, Tier = tier, Value = value};
    }
}
