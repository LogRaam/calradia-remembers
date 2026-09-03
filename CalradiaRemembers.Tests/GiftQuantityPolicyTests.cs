// Code written by Gabriel Mailhot, 03/09/2026.
// GiftQuantityPolicy backs give_item's transfer count. A player reported giving "10 ingots" and losing only one,
// because the transfer was hard-coded to a single unit. Get this rule wrong and either the count is ignored again
// (the report returns) or the player gives MORE than they hold (a dupe/negative-stock bug). These tests pin the
// clamp: the asked-for count, never above the stack, never below one, and zero only when the player holds none.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class GiftQuantityPolicyTests
    {
        // THE PLAYER REPORT: the count the player states must actually move when they hold enough of the stack.
        [Test]
        public void GIVEN_a_count_within_the_stack_WHEN_resolving_THEN_that_many_move()
        {
            GiftQuantityPolicy.Resolve(requested: 10, available: 10).Should().Be(10);
            GiftQuantityPolicy.Resolve(requested: 3, available: 10).Should().Be(3);
        }

        // STAKE: you cannot give what you do not have. Asking for more than the stack gives the whole stack, never
        // a negative or duped amount (fkasad had 10 and asked 10; a player with 9 asking 10 should give 9).
        [Test]
        public void GIVEN_a_count_above_the_stack_WHEN_resolving_THEN_it_gives_the_whole_stack()
        {
            GiftQuantityPolicy.Resolve(requested: 10, available: 9).Should().Be(9);
            GiftQuantityPolicy.Resolve(requested: 100, available: 3).Should().Be(3);
        }

        // STAKE: a plain "I give you my sword" names no count, so an unspecified (or garbage-parsed 0/negative)
        // request must default to a single unit, never zero, or a normal one-item gift would silently move nothing.
        [TestCase(0)]
        [TestCase(-5)]
        public void GIVEN_no_or_invalid_count_WHEN_resolving_THEN_it_defaults_to_one(int requested)
        {
            GiftQuantityPolicy.Resolve(requested, available: 5).Should().Be(1);
        }

        // STAKE: when the player holds none, the result is 0 so the caller refuses instead of transferring a
        // phantom unit (the "you don't have that" path, not a silent give of nothing).
        [Test]
        public void GIVEN_an_empty_stack_WHEN_resolving_THEN_it_is_zero()
        {
            GiftQuantityPolicy.Resolve(requested: 4, available: 0).Should().Be(0);
            GiftQuantityPolicy.Resolve(requested: 0, available: 0).Should().Be(0);
        }
    }
}
