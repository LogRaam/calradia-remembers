// Code written by Gabriel Mailhot, 08/07/2026.
// This is the pure core of the ProvideGoods supply quest (referential increment 2): the LLM writes a free-text
// category word for what a lord wants, and this policy normalizes it to one of three canonical keys that
// SupplyItemMatcher then maps to real game items at runtime. Get normalization wrong and the runtime matcher
// (which switches on these exact string constants) silently never counts a delivered item, stalling a quest
// the player has genuinely fulfilled. An unrecognized category makes QuestFactory refuse the quest outright
// (a giver never asks for goods the game cannot verify), so Normalize's null path is a hard gate, not a detail.
// ClampCount and IsSatisfied protect the same "verified by a real hand-over, never the model's word" guarantee.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class ProvideGoodsPolicyTests
    {
        // SupplyItemMatcher switches on these exact canonical constants; any synonym that normalizes to
        // something else falls through its switch to "not a match", and no delivered item is ever counted
        // toward the quest.
        [TestCase("horse", ProvideGoodsPolicy.Horses)]
        [TestCase("Horses", ProvideGoodsPolicy.Horses)]
        [TestCase("war horse", ProvideGoodsPolicy.Horses)]
        [TestCase("cattle", ProvideGoodsPolicy.Livestock)]
        [TestCase("SHEEP", ProvideGoodsPolicy.Livestock)]
        [TestCase("grain", ProvideGoodsPolicy.Grain)]
        public void GIVEN_a_known_category_word_WHEN_normalizing_THEN_it_maps_to_the_canonical_key(string raw, string expected)
        {
            ProvideGoodsPolicy.Normalize(raw).Should().Be(expected);
        }

        // fearente (Nexus, 29/10/2026): asked for food, he handed over grain, then fish and meat, and only the grain
        // counted because "food" was read as grain. A lord who asks for food or provisions takes any food; one who asks
        // for grain or wheat still means grain.
        [TestCase("food", ProvideGoodsPolicy.Food)]
        [TestCase("Provisions", ProvideGoodsPolicy.Food)]
        [TestCase("wheat", ProvideGoodsPolicy.Grain)]
        public void GIVEN_a_lord_asking_for_food_WHEN_normalizing_THEN_any_food_is_asked_and_grain_still_means_grain(string raw, string expected)
        {
            ProvideGoodsPolicy.Normalize(raw).Should().Be(expected);
        }

        // A null category means QuestFactory refuses to create the quest at all (case ProvideGoods returns
        // false): a lord must never end up asking for goods the game has no way to recognize or verify.
        [TestCase("swords")]
        [TestCase("")]
        [TestCase(null)]
        public void GIVEN_an_unsupported_or_empty_category_WHEN_normalizing_THEN_it_returns_null(string raw)
        {
            ProvideGoodsPolicy.Normalize(raw).Should().BeNull();
        }

        // MinCount/MaxCount are TUNE constants (a lord does not ask for a herd of a thousand); this pins the
        // band so a retune can't silently let an issued quest demand something absurd or impossible.
        [Test]
        public void GIVEN_a_count_below_or_above_the_band_WHEN_clamping_THEN_it_is_held_inside()
        {
            ProvideGoodsPolicy.ClampCount(0).Should().Be(ProvideGoodsPolicy.MinCount);
            ProvideGoodsPolicy.ClampCount(-5).Should().Be(ProvideGoodsPolicy.MinCount);
            ProvideGoodsPolicy.ClampCount(99999).Should().Be(ProvideGoodsPolicy.MaxCount);
            ProvideGoodsPolicy.ClampCount(20).Should().Be(20);
        }

        // Completion is a strict count comparison against real handed-over goods, the "verified by a real
        // hand-over, never the model's word" guarantee this whole quest type exists to deliver.
        [Test]
        public void GIVEN_a_tally_at_or_over_the_required_count_WHEN_testing_THEN_it_is_satisfied()
        {
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 15, requiredCount: 15).Should().BeTrue();
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 16, requiredCount: 15).Should().BeTrue();
        }

        // A zero (or negative) required count must never read as trivially satisfied: a malformed quest
        // state should stay stuck rather than silently auto-complete and pay out a reward for nothing.
        [Test]
        public void GIVEN_a_tally_short_of_the_count_or_a_zero_requirement_WHEN_testing_THEN_it_is_not_satisfied()
        {
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 14, requiredCount: 15).Should().BeFalse();
            ProvideGoodsPolicy.IsSatisfied(handedOverCount: 5, requiredCount: 0).Should().BeFalse();
        }
    }
}
