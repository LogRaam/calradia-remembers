// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 companion hardening. Decides how a running council stipend CLOSES on the daily tick, fixing a
// silent-loss bug (audit MAJEUR): the escrow behavior removed a dead payee's stipend with no refund and no
// notice, swallowing up to the whole unpaid escrow (funded FROM the player up front, so a dead payee must not
// keep it), and it ended a completed tranche with a log line the player never saw. This policy names the two
// closures the behavior must surface, and computes the exact denars to hand back when the payee is gone.

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>How an active stipend ends on a given tick: it runs on, its tranche completed, or its payee is gone.</summary>
    public enum StipendEnd
    {
        /// <summary>The stipend continues: days remain and the escrow still holds coin.</summary>
        None,

        /// <summary>The tranche ran its course (days elapsed or escrow emptied) with the payee still alive.</summary>
        Completed,

        /// <summary>The payee died or vanished before the tranche ended: the unspent escrow must be refunded.</summary>
        PayeeGone
    }

    /// <summary>Pure closure decision for CouncilStipendBehavior: classify the end and size the payee-death refund.</summary>
    public static class StipendClosurePolicy
    {
        /// <summary>
        ///   Classifies how a stipend stands after a day's advance. A missing payee is always
        ///   <see cref="StipendEnd.PayeeGone" /> (refund due), whatever days or escrow remain. Otherwise it is
        ///   <see cref="StipendEnd.Completed" /> once the days run out or the escrow empties, else
        ///   <see cref="StipendEnd.None" />.
        /// </summary>
        public static StipendEnd Classify(bool payeeAvailable, int daysRemaining, int escrowBalance)
        {
            if (!payeeAvailable) return StipendEnd.PayeeGone;
            if (daysRemaining <= 0 || escrowBalance <= 0) return StipendEnd.Completed;

            return StipendEnd.None;
        }

        /// <summary>
        ///   The denars to return to the player when the payee is gone: the WHOLE unpaid remainder still in escrow,
        ///   never negative. The tranche was financed from the player's own purse up front, so a payee who dies
        ///   before it is spent must not carry the balance to the grave (audit: it was silently swallowed).
        /// </summary>
        public static int RefundOnPayeeGone(int escrowBalance)
            => escrowBalance > 0 ? escrowBalance : 0;
    }
}
