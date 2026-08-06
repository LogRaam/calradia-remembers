// Code written by Gabriel Mailhot, 18/07/2026.
// When lent soldiers go home, and how many of them are left to go. Pure: the host reads the quest and the
// player's roster, this decides what that means. Increment 0 of the troop-loan pillar, deliberately PASSIVE:
// nothing calls it yet, so the rule can be read in tests before a single soldier moves in game.
//
// Ratified with Gabriel 2026-07-18, and every boundary below is one of his calls:
//   - The loan ends when the DEED is done, not when the player returns to collect the reward. A quest that
//     stops being OUTSTANDING is the signal, which covers success and every failure path alike, so failing
//     a task can never become the way to keep the men.
//   - Casualties carry NO consequence. Whatever survives goes back, and nothing judges the difference.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    // DEAD, PENDING v1.34.2 (marked 2026-08-06). Still wired to NOTHING: no production code calls ShouldReturn,
    // ReturnableCounts, or LoansToReturn (verified by grep). The v1.34.1 hotfix made the 1:1 lend_troops verb
    // HONEST by shipping it as a PERMANENT reinforcement (a gift the player keeps), so no soldier is ever
    // returned and this return-logic is exercised only by its own tests. Do NOT delete it or its tests here:
    // v1.34.2 is the deliberate decision to either wire a real returnable loan onto this policy or remove it.
    // The tests below still assert the "loan returns" contract, which currently matches no shipped behaviour.

    /// <summary>Decides the fate of a <see cref="TroopLoan" /> when the task it served moves on.</summary>
    public static class TroopLoanPolicy
    {
        /// <summary>
        ///   True when these soldiers must go home now.
        /// </summary>
        /// <param name="loan">The loan on the lender's profile.</param>
        /// <param name="questIsOutstanding">
        ///   <c>InformalQuest.IsOutstanding</c> for the quest this loan serves: Active AND not yet satisfied.
        ///   It going false is the whole trigger, and it is deliberately the same flag for a hideout cleared
        ///   and for a deadline missed. A quest the host can no longer find counts as not outstanding: the
        ///   task is over one way or another, and stranding the men with the player forever is the one
        ///   outcome with no defence.
        /// </param>
        public static bool ShouldReturn(TroopLoan loan, bool questIsOutstanding)
            => loan != null && loan.IsOpen && !questIsOutstanding;

        /// <summary>
        ///   What actually marches home, per troop type: never more than was lent, never more than the player
        ///   still has. The shortfall is simply the dead, and it is NOT reported, penalised, or remembered
        ///   (Gabriel 2026-07-18: the quest's own outcome is the consequence, the lender does not also grieve
        ///   the arithmetic). A type the player no longer has any of is dropped rather than returned as zero,
        ///   so the caller can treat an empty result as "nothing came back".
        /// </summary>
        /// <param name="lent">What the loan handed over: troop id to count.</param>
        /// <param name="stillInPlayerParty">What the player's roster holds now: troop id to count.</param>
        public static IReadOnlyDictionary<string, int> ReturnableCounts(
            IReadOnlyDictionary<string, int> lent,
            IReadOnlyDictionary<string, int> stillInPlayerParty)
        {
            var result = new Dictionary<string, int>();
            if (lent == null) return result;

            foreach (KeyValuePair<string, int> entry in lent)
            {
                if (entry.Value <= 0) continue;

                int available = 0;
                if (stillInPlayerParty != null && stillInPlayerParty.TryGetValue(entry.Key, out int held))
                    available = held;

                int going = entry.Value < available
                    ? entry.Value
                    : available;

                if (going > 0) result[entry.Key] = going;
            }

            return result;
        }

        /// <summary>
        ///   The loans on one lender's profile that are ready to go home, given which quest keys are still
        ///   outstanding. Kept here rather than in the caller so the "which loans" and "how many men" halves
        ///   of the rule stay side by side and testable as one chain.
        /// </summary>
        public static IReadOnlyList<TroopLoan> LoansToReturn(IEnumerable<TroopLoan> loans,
                                                             ICollection<string> outstandingQuestKeys)
        {
            var due = new List<TroopLoan>();
            if (loans == null) return due;

            foreach (TroopLoan loan in loans)
            {
                bool outstanding = loan?.QuestKey != null
                                && outstandingQuestKeys != null
                                && outstandingQuestKeys.Contains(loan.QuestKey);

                if (ShouldReturn(loan, outstanding)) due.Add(loan);
            }

            return due;
        }
    }
}
