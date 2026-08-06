// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 companion hardening. StipendClosurePolicy decides how CouncilStipendBehavior.Advance ENDS a running
// stipend each day: keep paying, close a completed tranche, or refund a dead payee's unspent escrow. Before this,
// the behavior removed a dead payee's stipend with no refund and no notice, silently swallowing up to the whole
// remaining escrow (~15k denars possible) that had been financed from the PLAYER's own purse up front; and a
// normal completion ended with only a log line the player never saw. These tests pin the classification and the
// refund amount, since a wrong answer either strands the player's already-spent gold or ends an escrow unseen.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class StipendClosurePolicyTests
    {
        // The core silent-loss fix: a payee who has died (unavailable) must classify as PayeeGone REGARDLESS of
        // how many days or how much escrow remain, so the behavior refunds instead of quietly dropping the stipend.
        [Test]
        public void GIVEN_the_payee_is_gone_mid_tranche_WHEN_classified_THEN_it_is_PayeeGone_so_the_escrow_is_refunded_not_swallowed()
        {
            StipendClosurePolicy.Classify(false, 17, 2040).Should().Be(StipendEnd.PayeeGone);
        }

        // A live payee with days AND escrow left keeps the stipend running: paying out normally, never closing early.
        [Test]
        public void GIVEN_a_live_payee_with_days_and_escrow_left_WHEN_classified_THEN_it_continues()
        {
            StipendClosurePolicy.Classify(true, 17, 2040).Should().Be(StipendEnd.None);
        }

        // The tranche is complete once its days run out: the behavior must NOTIFY the player it ended, not just log.
        [Test]
        public void GIVEN_a_live_payee_whose_days_have_elapsed_WHEN_classified_THEN_it_is_Completed()
        {
            StipendClosurePolicy.Classify(true, 0, 500).Should().Be(StipendEnd.Completed);
        }

        // An emptied escrow also completes the tranche even with days nominally left, matching the behavior's
        // "days or escrow, whichever comes first" end condition.
        [Test]
        public void GIVEN_a_live_payee_whose_escrow_is_empty_WHEN_classified_THEN_it_is_Completed()
        {
            StipendClosurePolicy.Classify(true, 5, 0).Should().Be(StipendEnd.Completed);
        }

        // The refund must be the WHOLE unspent remainder: it was escrowed from the player, so a dead payee returns
        // every denar not yet paid out. A short refund would silently pocket the difference (the audit bug).
        [Test]
        public void GIVEN_a_payee_gone_with_escrow_left_WHEN_sizing_the_refund_THEN_it_is_the_whole_unpaid_remainder()
        {
            StipendClosurePolicy.RefundOnPayeeGone(2040).Should().Be(2040);
        }

        // Guard against a negative escrow ever paying the player a phantom sum: the refund floors at zero.
        [Test]
        public void GIVEN_a_non_positive_escrow_WHEN_sizing_the_refund_THEN_it_is_zero_never_negative()
        {
            StipendClosurePolicy.RefundOnPayeeGone(0).Should().Be(0);
            StipendClosurePolicy.RefundOnPayeeGone(-5).Should().Be(0);
        }
    }
}
