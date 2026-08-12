// Code written by Gabriel Mailhot, 12/08/2026.
// Decides how a FAILED or ABANDONED informal quest is charged against the giver's regard and memory. Before this,
// every quest failure (an open abandonment, or a lapsed deadline) planted a knownToPlayer FailedQuest grudge,
// recorded a "broken word" Conflict memory, spent promise-trust, and docked full regard. For a bandit deed whose
// target can hide until physically scouted (a hideout stays hidden, or the open-field bandits may have moved on),
// that punished the player for honestly reporting they could not find an unfindable foe: the giver turned
// disgusted and hostile (player report). This pure rule softens ONLY the hard-to-find bandit class to a mild
// disappointment (no grudge, no broken-word memory, no trust spent, a token regard nick), while every other quest
// type keeps the full broken-word consequence with the CALLER's own penalty constant. Kept engine-free and
// dependency-light so it stays tested; the caller applies the result against the live profile and grudge ledger.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How a failed or abandoned quest is charged: whether it grudges, remembers a broken word, spends trust, and by how much regard.</summary>
    public sealed class QuestFailureLeniency
    {
        /// <summary>Creates a leniency verdict.</summary>
        public QuestFailureLeniency(bool plantGrudge, bool recordBrokenWordMemory, bool spendPromiseTrust, int regardPenalty)
        {
            PlantGrudge = plantGrudge;
            RecordBrokenWordMemory = recordBrokenWordMemory;
            SpendPromiseTrust = spendPromiseTrust;
            RegardPenalty = regardPenalty;
        }

        /// <summary>True to plant a resolvable FailedQuest grudge against the player (a lasting, known slight).</summary>
        public bool PlantGrudge { get; }

        /// <summary>True to record a "broken word" Conflict memory on the giver.</summary>
        public bool RecordBrokenWordMemory { get; }

        /// <summary>True to spend promise-trust via the stance system (a broken promise). False when failing an unfindable target is not a broken promise.</summary>
        public bool SpendPromiseTrust { get; }

        /// <summary>The personal-regard hit to apply (magnitude, the caller negates it).</summary>
        public int RegardPenalty { get; }
    }

    /// <summary>Decides the leniency of a quest failure by kind: hard-to-find bandit deeds are forgiven, all else keeps full consequence.</summary>
    public static class QuestFailureLeniencyPolicy
    {
        /// <summary>
        ///   The token regard nick for a failed hard-to-find bandit deed: a mild disappointment, not a betrayal.
        ///   Exposed as a named const so it can be tuned (set to 0 to make bandit failures entirely cost-free).
        /// </summary>
        public const int BanditFailureRegardPenalty = 1;

        /// <summary>
        ///   True for the deed kinds whose target can prove genuinely hard to find (a hideout stays hidden until
        ///   scouted; open-field bandits may have moved on), so failing to find one is not the player's fault.
        /// </summary>
        public static bool IsHardToFindTarget(QuestType type)
            => type == QuestType.BanditClear || type == QuestType.BanditHideout;

        /// <summary>
        ///   The leniency verdict for a failed/abandoned quest of <paramref name="type" />.
        ///   <paramref name="defaultRegardPenalty" /> is the caller's own full penalty for this failure path (its
        ///   AbandonPenalty or DeadlineFailurePenalty), echoed back for non-bandit types so the policy never
        ///   duplicates those constants. A hard-to-find bandit deed instead returns the flat, gentle
        ///   <see cref="BanditFailureRegardPenalty" /> with no grudge, no broken-word memory, and no trust spent.
        /// </summary>
        public static QuestFailureLeniency Resolve(QuestType type, int defaultRegardPenalty)
        {
            if (IsHardToFindTarget(type))
                return new QuestFailureLeniency(false, false, false, BanditFailureRegardPenalty);

            return new QuestFailureLeniency(true, true, true, defaultRegardPenalty);
        }
    }
}
