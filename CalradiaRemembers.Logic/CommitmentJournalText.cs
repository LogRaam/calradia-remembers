// Code written by Gabriel Mailhot, 03/07/2026.
// RP with teeth, increment 2, Phase 2: pure text for the vanilla quest-journal mirror of a
// TrackedCommitment. Kept independent of CalradiaRemembers.Commitments.CommitmentQuest (the mod's
// QuestBase mirror) so this project stays engine-agnostic; the mod resolves the live promisor/rival
// names and calls these builders in.
// 04/08/2026: R7-light (swear_oath) adds a kind-aware overload of each builder: the original two-name
// (promisorName, rivalName) overloads are UNCHANGED (still exactly what pledge_against's own MoveAgainst
// mirror calls, and what the existing tests pin), since only the OTHER three kinds need a different clause
// ("pay the player N denars" reads nothing like "move against a rival").

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Pure formatting for the vanilla quest-journal mirror of a pledge (pledge_against /
    ///   <see cref="TrackedCommitment" />): a stable title and one log line per lifecycle transition.
    ///   No engine types in or out: a straight (promisorName, rivalName[, state]) to string mapping,
    ///   so the wording is documented by the tests instead of read out of the mod.
    /// </summary>
    public static class CommitmentJournalText
    {
        /// <summary>The journal entry's title, e.g. "Pledge: Derthert".</summary>
        public static string BuildTitle(string promisorName)
            => $"Pledge: {NameOrFallback(promisorName)}";

        /// <summary>Log line for the pledge as freshly made: the promisor just vowed to move against the rival.</summary>
        public static string BuildOpenedLine(string promisorName, string rivalName)
            => $"{NameOrFallback(promisorName)} pledged to move against {RivalOrFallback(rivalName)}.";

        /// <summary>Closing log line: the deadline lapsed with nothing done, an oath broken.</summary>
        public static string BuildBreachedLine(string promisorName, string rivalName)
            => $"{NameOrFallback(promisorName)} pledged to move against {RivalOrFallback(rivalName)} and did nothing.";

        /// <summary>Closing log line: the pledge was honored before the deadline.</summary>
        public static string BuildFulfilledLine(string promisorName, string rivalName)
            => $"{NameOrFallback(promisorName)} kept their word and moved against {RivalOrFallback(rivalName)}.";

        /// <summary>
        ///   Dispatches to the right log line by lifecycle <paramref name="state" />: Fulfilled and
        ///   Breached get their own closing line, anything else (i.e. still Active) reads as the opening
        ///   line — the one state a mirror is ever newly created in.
        /// </summary>
        public static string BuildLogLine(CommitmentState state, string promisorName, string rivalName)
        {
            switch (state)
            {
                case CommitmentState.Fulfilled:
                    return BuildFulfilledLine(promisorName, rivalName);
                case CommitmentState.Breached:
                    return BuildBreachedLine(promisorName, rivalName);
                default:
                    return BuildOpenedLine(promisorName, rivalName);
            }
        }

        /// <summary>swear_oath's own opening line: the promisor just swore an oath of the given kind.</summary>
        public static string BuildOpenedLine(CommitmentKind kind, string promisorName, string targetName, int? amount)
            => $"{NameOrFallback(promisorName)} swore to {DescribeOathClause(kind, targetName, amount)}.";

        /// <summary>swear_oath's own breached line: the deadline lapsed (or, for KeepPeace, war was declared) with the oath broken.</summary>
        public static string BuildBreachedLine(CommitmentKind kind, string promisorName, string targetName, int? amount)
            => $"{NameOrFallback(promisorName)} swore to {DescribeOathClause(kind, targetName, amount)}, and broke that oath.";

        /// <summary>swear_oath's own fulfilled line: the oath was honored before the deadline (or, for KeepPeace, by reaching it unbroken).</summary>
        public static string BuildFulfilledLine(CommitmentKind kind, string promisorName, string targetName, int? amount)
            => $"{NameOrFallback(promisorName)} kept their oath to {DescribeOathClause(kind, targetName, amount)}.";

        /// <summary>Kind-aware dispatcher, mirroring <see cref="BuildLogLine(CommitmentState, string, string)" /> for the three swear_oath kinds.</summary>
        public static string BuildLogLine(CommitmentState state, CommitmentKind kind, string promisorName, string targetName, int? amount)
        {
            switch (state)
            {
                case CommitmentState.Fulfilled:
                    return BuildFulfilledLine(kind, promisorName, targetName, amount);
                case CommitmentState.Breached:
                    return BuildBreachedLine(kind, promisorName, targetName, amount);
                default:
                    return BuildOpenedLine(kind, promisorName, targetName, amount);
            }
        }

        private static string NameOrFallback(string promisorName)
            => string.IsNullOrWhiteSpace(promisorName)
                ? "They"
                : promisorName;

        private static string RivalOrFallback(string rivalName)
            => string.IsNullOrWhiteSpace(rivalName)
                ? "their rival"
                : rivalName;

        // The oath's own clause, e.g. "pay you 200 denars", "keep the peace with Vlandia", "stand at your side in battle".
        // MoveAgainst is deliberately absent here: that kind is pledge_against's own separate resolution, and its
        // journal mirror always goes through the ORIGINAL two-name overloads above, never this dispatcher.
        private static string DescribeOathClause(CommitmentKind kind, string targetName, int? amount)
        {
            switch (kind)
            {
                case CommitmentKind.PayGold:
                    return amount is > 0 ? $"pay you {amount} denars" : "pay you a sum of denars";
                case CommitmentKind.KeepPeace:
                    return string.IsNullOrWhiteSpace(targetName) ? "keep the peace" : $"keep the peace with {targetName}";
                case CommitmentKind.Protect:
                    return "stand at your side in battle";
                default:
                    return "honor a sworn oath";
            }
        }
    }
}
