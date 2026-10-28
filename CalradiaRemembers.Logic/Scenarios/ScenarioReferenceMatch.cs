// Code written by Gabriel Mailhot, 11/10/2026.
// A scenario rule names heroes and kingdoms by a reference: a StringId ("empire_s", "lord_1_14") or a name
// ("Rhagaea"). Found by cr.test scenario_death (11/10/2026): the first candidate whose name merely CONTAINED the
// reference won, so "empire" (the Northern Empire's id) picked "Southern Empire" when the South came first, and a
// war against oneself was skipped. Names are also translated by the game, so a pack that must work in every
// language names by id, and an id must win over any name. The best match wins: exact id, then exact name, then a
// name that contains the reference; among equals, the first candidate (the event's own heroes come first).

namespace CalradiaRemembers.Logic.Scenarios
{
    /// <summary>One hero or faction a reference may name.</summary>
    public sealed class ScenarioCandidate
    {
        /// <summary>The game's StringId.</summary>
        public string Id { get; init; }

        /// <summary>The displayed name, in the player's language.</summary>
        public string Name { get; init; }
    }

    /// <summary>How well a scenario reference names a candidate, and which candidate it names. Pure.</summary>
    public static class ScenarioReferenceMatch
    {
        /// <summary>No match.</summary>
        public const int None = 0;

        /// <summary>The name contains the reference ("Rhagaea" in "Rhagaea Pethros").</summary>
        public const int NameContains = 1;

        /// <summary>The name is the reference.</summary>
        public const int ExactName = 2;

        /// <summary>The StringId is the reference.</summary>
        public const int ExactId = 3;

        /// <summary>How well <paramref name="reference" /> names <paramref name="candidate" />, case-insensitively.</summary>
        public static int Rank(ScenarioCandidate candidate, string reference)
        {
            string r = reference?.Trim();
            if (candidate == null || string.IsNullOrEmpty(r)) return None;
            if (!string.IsNullOrEmpty(candidate.Id) && string.Equals(candidate.Id, r, System.StringComparison.OrdinalIgnoreCase)) return ExactId;
            if (string.IsNullOrEmpty(candidate.Name)) return None;
            if (string.Equals(candidate.Name, r, System.StringComparison.OrdinalIgnoreCase)) return ExactName;

            return candidate.Name.IndexOf(r, System.StringComparison.OrdinalIgnoreCase) >= 0 ? NameContains : None;
        }

        /// <summary>The index of the candidate the reference names best (the first among equals), or -1 when none.</summary>
        public static int BestIndex(System.Collections.Generic.IReadOnlyList<ScenarioCandidate> candidates, string reference)
        {
            if (candidates == null) return -1;

            int best = -1;
            int bestRank = None;
            for (int i = 0; i < candidates.Count; i++)
            {
                int rank = Rank(candidates[i], reference);
                if (rank <= bestRank) continue;

                best = i;
                bestRank = rank;
                if (rank == ExactId) break;
            }

            return best;
        }
    }
}
