// Code written by Gabriel Mailhot, 04/07/2026.
// Chronicle: notable intimate conquests. A player asked that a memorable intimate scene with a hero of
// standing (a captive lord/lady taken, or an heir begotten upon one) be chronicle-worthy, like marriage
// or childbirth already are. Pure so the gating (adult content off, or the partner being a nobody) and
// the register (tasteful at Mature, bawdier at Hardcore) stay protected by tests. Never explicit: the line
// is evocative period prose, meant to sit beside the marriage/execution/siege lines in the same chronicle.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Which kind of intimate beat is being considered for the chronicle.</summary>
    public enum IntimateChronicleSceneKind
    {
        /// <summary>A captive lord or lady of standing, taken in a Hardcore intimate captive scene.</summary>
        CaptiveConquest,

        /// <summary>An heir conceived with a hero of standing (the conception/begetting beat, not the birth).</summary>
        Conception
    }

    /// <summary>
    ///   Decides whether a notable intimate event is worth a chronicle line, and composes that line's
    ///   text. Gated on <see cref="AdultContentLevel" /> (nothing at all when adult content is Off) and on
    ///   the partner being a hero of standing (a lord/lady/notable) — a random bandit-camp captive or
    ///   commoner never earns a line here. Register: Mature and Explicit both read as tasteful, period
    ///   euphemism; Hardcore alone is bawdier, still never explicit.
    /// </summary>
    public static class IntimateChroniclePolicy
    {
        /// <summary>
        ///   Builds the chronicle line for this beat, or null when it does not qualify: adult content is
        ///   Off, the partner is not a hero of standing, or either name is missing.
        /// </summary>
        public static string BuildLine(
            AdultContentLevel adultLevel,
            bool partnerIsNotable,
            IntimateChronicleSceneKind sceneKind,
            string playerName,
            string partnerName,
            bool partnerIsFemale)
        {
            if (adultLevel < AdultContentLevel.Mature) return null;
            if (!partnerIsNotable) return null;
            if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(partnerName)) return null;

            bool bawdy = adultLevel >= AdultContentLevel.Hardcore;
            string honorific = partnerIsFemale ? "lady" : "lord";

            return sceneKind switch {
                IntimateChronicleSceneKind.CaptiveConquest => CaptiveConquestLine(bawdy, honorific, playerName, partnerName),
                IntimateChronicleSceneKind.Conception => ConceptionLine(bawdy, playerName, partnerName, partnerIsFemale),
                _ => null
            };
        }

        private static string CaptiveConquestLine(bool bawdy, string honorific, string playerName, string partnerName)
            => bawdy
                ? $"{playerName} took the proud {honorific} {partnerName} to bed as a captive, and left no doubt who was master now"
                : $"{playerName} made a conquest of the proud {honorific} {partnerName}, held captive and humbled in secret";

        /// <summary>
        ///   Under the game's own conception rule only one of the pair can be female (heterosexual
        ///   pairing), so <paramref name="partnerIsFemale" /> alone tells us which of the two is the
        ///   mother: the female one carries the child, whichever side of the pairing that is.
        /// </summary>
        private static string ConceptionLine(bool bawdy, string playerName, string partnerName, bool partnerIsFemale)
        {
            string motherName = partnerIsFemale ? partnerName : playerName;
            string fatherName = partnerIsFemale ? playerName : partnerName;

            return bawdy
                ? $"{fatherName} got {motherName} with child in a bed well and truly shared, an heir now surely on the way"
                : $"an heir was got upon {motherName} by {fatherName}, and the two houses would be bound by more than words";
        }
    }
}
