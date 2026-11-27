// Code written by Gabriel Mailhot, 26/11/2026.
// RegardTakenLedger: how much regard each character took from the player in this conversation and still holds, which
// RelationGate may give back when the character warms again. Lordfadooboo (Nexus, 26/11/2026): a character who accused him
// falsely and then admitted it kept every point it had taken. If the ledger is wrong, the points never come back, come back
// twice, or come back from another character's loss.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RegardTakenLedgerTests
    {
        // STAKE: two cold turns, then the admission: everything taken is owed.
        [Test]
        public void GIVEN_two_losses_WHEN_read_THEN_both_are_held()
        {
            var ledger = new RegardTakenLedger();
            ledger.Note("lord_a", -3);
            ledger.Note("lord_a", -2);

            ledger.TakenFrom("lord_a").Should().Be(5);
        }

        // STAKE: what was given back is no longer owed; a second warm turn must not return it twice.
        [Test]
        public void GIVEN_regard_given_back_WHEN_read_THEN_only_the_rest_is_held_and_never_below_zero()
        {
            var ledger = new RegardTakenLedger();
            ledger.Note("lord_a", -5);
            ledger.Note("lord_a", 3);
            ledger.TakenFrom("lord_a").Should().Be(2);

            ledger.Note("lord_a", 4);
            ledger.TakenFrom("lord_a").Should().Be(0);
        }

        // STAKE: one character's harsh words are not another's to repay.
        [Test]
        public void GIVEN_a_loss_from_one_character_WHEN_another_is_read_THEN_nothing_is_owed_by_them()
        {
            var ledger = new RegardTakenLedger();
            ledger.Note("lord_a", -4);

            ledger.TakenFrom("lord_b").Should().Be(0);
        }
    }
}
