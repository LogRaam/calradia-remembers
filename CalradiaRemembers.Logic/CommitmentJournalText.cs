// Code written by Gabriel Mailhot, 03/07/2026.
// RP with teeth, increment 2, Phase 2: pure text for the vanilla quest-journal mirror of a
// TrackedCommitment. Kept independent of CalradiaRemembers.Commitments.CommitmentQuest (the mod's
// QuestBase mirror) so this project stays engine-agnostic; the mod resolves the live promisor/rival
// names and calls these builders in.

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

        private static string NameOrFallback(string promisorName)
            => string.IsNullOrWhiteSpace(promisorName)
                ? "They"
                : promisorName;

        private static string RivalOrFallback(string rivalName)
            => string.IsNullOrWhiteSpace(rivalName)
                ? "their rival"
                : rivalName;
    }
}
