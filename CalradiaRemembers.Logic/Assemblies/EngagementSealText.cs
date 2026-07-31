// Code written by Gabriel Mailhot, 31/07/2026.
// Real world-effect executors (grant fief, gold, etc.) do not exist yet in this codebase (only a bandit-clear
// quest is executable, see CouncilLift.Settle), so an AGREED engagement's apply-at-End is a durable MEMORY seal
// rather than a mechanical effect: the concerned hero remembers what the table decided. The wording is pinned
// here, pure, so it stays first person on the bound member ("I agreed to...", matching the mod's first-person
// [EVENT] memory convention) and third person when the subject could not be resolved to a seated member and the
// decision is sealed onto the presiding anchor instead ("it was agreed that...").

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Pins the durable memory wording sealed onto a hero when a council engagement is applied at the lift.</summary>
    public static class EngagementSealText
    {
        /// <summary>
        ///   Builds the first-person memory summary for an engagement. Blank <paramref name="engagementText" />
        ///   returns "" (nothing to seal). A trailing period is dropped before the wrapping sentence is built, so
        ///   the result never carries a double period. Never throws.
        /// </summary>
        public static string Summarize(string engagementText, bool onBoundSubject)
        {
            if (string.IsNullOrWhiteSpace(engagementText)) return "";

            string clean = engagementText.Trim();
            if (clean.EndsWith(".")) clean = clean.Substring(0, clean.Length - 1);

            return onBoundSubject
                ? $"At the council, I agreed to {clean}."
                : $"At the council, it was agreed that {clean}.";
        }
    }
}
