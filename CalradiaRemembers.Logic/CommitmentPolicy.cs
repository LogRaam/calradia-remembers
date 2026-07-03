// Code written by Gabriel Mailhot, 02/07/2026.
// RP with teeth, increment 2, Phase 1: pure deadline math and consequence magnitudes for
// TrackedCommitment. The engine-coupled parts (creating the commitment, detecting fulfillment via a
// resolved scheme or a real battle, detecting a breach on the daily tick) live in
// CalradiaRemembers.Commitments.CommitmentBehavior; this class only reasons over plain numbers, so the
// tuning stays protected by tests.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure math and tuning for a TrackedCommitment's deadline and its fulfilled/breached consequences.</summary>
    public static class CommitmentPolicy
    {
        /// <summary>Tuning: how many in-game days a pledge has to be honored — roughly one season.</summary>
        public const int DeadlineDays = 21;

        /// <summary>Tuning: relation/stance boost applied to the promisor toward the player on fulfillment.</summary>
        public const int FulfilledRelationBonus = 6;

        /// <summary>Tuning: relation/stance penalty applied to the promisor toward the player on breach (negative).</summary>
        public const int BreachedRelationPenalty = -12;

        /// <summary>The deadline day for a commitment issued on <paramref name="issuedDay" />.</summary>
        public static int ComputeDeadline(int issuedDay) => issuedDay + DeadlineDays;

        /// <summary>True once <paramref name="currentDay" /> has reached or passed the deadline (inclusive).</summary>
        public static bool IsOverdue(int currentDay, int deadlineDay) => currentDay >= deadlineDay;
    }
}
