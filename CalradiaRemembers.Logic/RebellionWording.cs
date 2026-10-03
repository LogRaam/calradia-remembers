// Code written by Gabriel Mailhot, 02/10/2026.
// FloatingMammal (Nexus, 02/10/2026), after helping the Southern Empire crush a rebellion (Bellum Civile): a lady whose
// castle he took complained of it, "but she also didn't register that she was a rebel, claiming she fought on the
// Southern Empire's side (despite clearly having been a rebel against Rhagaea)... She's an honest lord so she shouldn't
// just be lying." CR recorded the PLAYER's own house changing sides, never anyone else's: once her house was received
// back, her prompt knew only her present allegiance. Every member of a house that rises now remembers it, and remembers
// the return; the words are here.

namespace CalradiaRemembers.Logic
{
    /// <summary>A house's rebellion, and its return to the fold.</summary>
    public sealed class RebellionFacts
    {
        /// <summary>The house's name.</summary>
        public string House { get; set; } = "";

        /// <summary>The realm it rose against (or returned to).</summary>
        public string Realm { get; set; } = "";

        /// <summary>That realm's ruler at the time, when known.</summary>
        public string Ruler { get; set; } = "";

        /// <summary>The banner the rebels fought under, when the rising formed a realm of its own; empty otherwise.</summary>
        public string Banner { get; set; } = "";
    }

    /// <summary>How a house's members remember a rebellion, and how the world tells it. Pure.</summary>
    public static class RebellionWording
    {
        /// <summary>What each member of the house remembers of the rising.</summary>
        public static string RoseMemory(RebellionFacts f)
        {
            string ruler = string.IsNullOrWhiteSpace(f?.Ruler) ? "" : $" and {f.Ruler}";
            string banner = string.IsNullOrWhiteSpace(f?.Banner) ? " in open rebellion" : $", fighting under the banner of {f.Banner}";

            return $"Our house, {f?.House}, rose against {f?.Realm}{ruler}{banner}. We were rebels.";
        }

        /// <summary>What each member remembers when the house is received back into the realm it rose against.</summary>
        public static string ReturnedMemory(RebellionFacts f)
            => $"Our house, {f?.House}, was received back into {f?.Realm} after rising against it. We fought as rebels, and that is not forgotten.";

        /// <summary>How the world tells the rising.</summary>
        public static string RoseSummary(RebellionFacts f) => $"House {f?.House} rose in rebellion against {f?.Realm}.";

        /// <summary>How the world tells the return.</summary>
        public static string ReturnedSummary(RebellionFacts f) => $"House {f?.House} returned to {f?.Realm}'s fold after its rebellion.";
    }
}
