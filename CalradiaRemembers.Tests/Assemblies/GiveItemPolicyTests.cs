// Code written by Gabriel Mailhot, 05/08/2026.
// Documents give_item's pure policy: the one rule that decides whether an item may ever be given away at all.
// The stake across every test here: a quest item (Value == 0, e.g. the Dragon Banner) must never be handed over
// by a council pledge, in either direction, exactly the exclusion CalradiaRemembers.Items.ItemPickerService
// already applies when the player picks something to give in a 1:1 exchange.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class GiveItemPolicyTests
    {
        // The ordinary case: a real item with a genuine denar value is always giveable.
        [Test]
        public void GIVEN_an_item_with_a_positive_value_WHEN_checked_THEN_it_is_giveable()
        {
            GiveItemPolicy.IsGiveableItem(150).Should().BeTrue();
        }

        // The exact stake: a quest item (Value == 0 in vanilla, e.g. the Dragon Banner) must never be given away
        // by a council pledge. If this returned true, a lord could "gift" a scripted quest prop the giver's own
        // roster only carries because of an active quest, an item that was never really theirs to give.
        [Test]
        public void GIVEN_an_item_with_a_zero_value_WHEN_checked_THEN_it_is_not_giveable()
        {
            GiveItemPolicy.IsGiveableItem(0).Should().BeFalse();
        }

        // Defensive: a malformed negative value (should never occur from a real ItemObject.Value, but the
        // predicate must not treat it as giveable either) is refused the same way a zero-value item is.
        [Test]
        public void GIVEN_an_item_with_a_negative_value_WHEN_checked_THEN_it_is_not_giveable()
        {
            GiveItemPolicy.IsGiveableItem(-5).Should().BeFalse();
        }

        // Boundary: the lowest positive value is still giveable, since only zero (or below) marks a quest item.
        [Test]
        public void GIVEN_an_item_worth_exactly_one_denar_WHEN_checked_THEN_it_is_giveable()
        {
            GiveItemPolicy.IsGiveableItem(1).Should().BeTrue();
        }
    }
}
