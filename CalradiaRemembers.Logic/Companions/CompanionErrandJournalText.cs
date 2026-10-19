// Code written by Gabriel Mailhot, 02/07/2026.
// ROADMAP "A personal ledger of quests the PLAYER gives out": pure text for the vanilla quest-journal
// mirror of a companion errand. Kept independent of CalradiaRemembers.Missions.CompanionMissionPhase
// (the mod's phase enum) so this project stays engine-agnostic; the mod maps its own phase enum onto
// CompanionErrandPhase when it calls in.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>
    ///   Where a companion's errand is in its out-to-back arc, for journal-text purposes only. Mirrors
    ///   CalradiaRemembers.Missions.CompanionMissionPhase one-for-one; the mod maps between the two.
    /// </summary>
    public enum CompanionErrandPhase
    {
        /// <summary>Riding to the destination town.</summary>
        Outbound,

        /// <summary>At the town, gathering word.</summary>
        Lingering,

        /// <summary>Riding back to the player.</summary>
        Returning,

        /// <summary>Waylaid and taken prisoner; the errand waits until they are free.</summary>
        AwaitingReunion
    }

    /// <summary>
    ///   Pure formatting for the companion-errand quest-journal mirror: a stable title and one status
    ///   line per phase / closing outcome. No engine types in or out: a straight (enum, string) to
    ///   string mapping, so the wording is documented by the tests instead of read out of the mod.
    /// </summary>
    public static class CompanionErrandJournalText
    {
        /// <summary>The journal entry's title, e.g. "Errand: Martira".</summary>
        public static string BuildTitle(string companionName)
            => $"Errand: {NameOrFallback(companionName)}";

        /// <summary>
        ///   Closing line when the companion was recalled and abandoned the errand (letter or immediate).
        ///   <paramref name="reason" /> is appended in parentheses when given (e.g. "as your letter ordered");
        ///   omitted entirely when null/blank, so an immediate recall reads as a clean single sentence.
        /// </summary>
        public static string BuildRecalledLine(string companionName, string reason)
        {
            string name = NameOrFallback(companionName);
            string suffix = string.IsNullOrWhiteSpace(reason)
                ? ""
                : $" ({reason.Trim()})";

            return $"{name} abandoned the errand and returned to you{suffix}.";
        }

        /// <summary>Closing line when the companion made it home and reported in.</summary>
        public static string BuildReturnedLine(string companionName)
            => $"{NameOrFallback(companionName)} has returned and reported in.";

        /// <summary>
        ///   One status line for the current phase of an in-progress errand. <paramref name="type" /> only
        ///   colours the Outbound line (what they rode out to do); the later phases read the same regardless
        ///   of errand type.
        /// </summary>
        public static string BuildStatusLine(CompanionMissionType type, CompanionErrandPhase phase, string companionName, string destinationName)
        {
            string name = NameOrFallback(companionName);
            string town = string.IsNullOrWhiteSpace(destinationName)
                ? "a town"
                : destinationName;

            switch (phase)
            {
                case CompanionErrandPhase.Outbound:
                    return $"{name} rides for {town} to {ErrandPurpose(type)}.";
                case CompanionErrandPhase.Lingering:
                    return $"{name} has reached {town}.";
                case CompanionErrandPhase.Returning:
                    return $"{name} is on the way back to you.";
                case CompanionErrandPhase.AwaitingReunion:
                    return $"{name} was waylaid and taken captive; the errand waits until they are freed.";
                default:
                    return $"{name} is away on an errand.";
            }
        }

        /// <summary>What the companion rode out to do, for the Outbound line. One verb phrase per errand type.</summary>
        private static string ErrandPurpose(CompanionMissionType type)
        {
            switch (type)
            {
                case CompanionMissionType.Spy: return "scout it";
                case CompanionMissionType.Steal: return "steal something worth bringing home";
                case CompanionMissionType.Barter: return "trade for a profit";
                case CompanionMissionType.Envoy: return "carry your word as an envoy";
                default: return "bring back news";
            }
        }

        private static string NameOrFallback(string companionName)
            => string.IsNullOrWhiteSpace(companionName)
                ? "Your companion"
                : companionName;
    }
}
