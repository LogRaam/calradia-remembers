// Code written by Gabriel Mailhot, 17/10/2026.
// A council dispatch_mission resolution grounds on the errand named (AssemblyResolutionEntry.TargetHint, one slot). A
// patrol also needs the PLACE the player named (jaserms1, 16/10/2026: a war council ordered a companion to patrol a
// village), so the slot carries "mission|settlement" for a patrol. Every entry recorded before the patrol existed is a
// bare errand name, and must still read as one: a delimiter-less hint is the mission alone (the reverse of
// QuestGroundingCodec, where a bare hint is the settlement).

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Composes/splits a council dispatch's (mission, settlement) pair into the ledger entry's one grounding slot.</summary>
    public static class DispatchGroundingCodec
    {
        private const char Delimiter = '|';

        /// <summary>"mission|settlement", or the bare mission when no place was named (the shape of every older entry).</summary>
        public static string Compose(string mission, string settlement)
            => string.IsNullOrWhiteSpace(settlement)
                ? mission ?? ""
                : $"{(mission ?? "").Trim()}{Delimiter}{settlement.Trim()}";

        /// <summary>Splits a hint into the mission and the place (null when none). A bare hint is the mission alone.</summary>
        public static void Decompose(string composite, out string mission, out string settlement)
        {
            mission = null;
            settlement = null;
            if (string.IsNullOrWhiteSpace(composite)) return;

            int idx = composite.IndexOf(Delimiter);
            if (idx < 0)
            {
                mission = composite.Trim();

                return;
            }

            string left = composite.Substring(0, idx).Trim();
            string right = composite.Substring(idx + 1).Trim();
            mission = left.Length > 0 ? left : null;
            settlement = right.Length > 0 ? right : null;
        }
    }
}
