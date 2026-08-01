// Code written by Gabriel Mailhot, 01/08/2026.
// The unit the mailbox scheduler (LetterMailboxPolicy) competes on: one unprompted letter a caller has
// already decided COULD be sent, waiting for its turn against the budget.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   One candidate letter waiting to compete for the mailbox budget. The weight is precomputed by the
    ///   caller (via <see cref="LetterWeights" />) so this policy stays agnostic of WHY a letter carries
    ///   the weight it does; it only ever compares numbers.
    /// </summary>
    public sealed class LetterCandidate
    {
        /// <summary>The subject that prompted this letter; kept for the caller's own bookkeeping and logging.</summary>
        public LetterReason Reason { get; set; }

        /// <summary>The NPC who would send this letter; also the final deterministic tie-break.</summary>
        public string SenderId { get; set; }

        /// <summary>
        ///   The candidate's base priority weight, precomputed by the caller (typically
        ///   <see cref="LetterWeights.BaseWeight" />, plus <see cref="LetterWeights.BattleVictoryWeight" />
        ///   for a battle congratulation). This policy never recomputes it, only ages and compares it.
        /// </summary>
        public int Weight { get; set; }

        /// <summary>The game day this letter first became eligible to send; the anchor for aging and tie-breaks.</summary>
        public int EligibleSinceDay { get; set; }
    }
}
