// Code written by Gabriel Mailhot, 10/07/2026.
// Player request (2026-07-10): let a player CHANGE an NPC's sexual orientation (e.g. to enable a female-female
// romance the rolled "heterosexual" was blocking). This maps a human word the player writes (in
// character_overrides.json or the cr.orientation console) to a SexualOrientation, or null when unrecognized.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Parses a human orientation word (plus common synonyms) to a <see cref="SexualOrientation" />.</summary>
    public static class OrientationParser
    {
        public static SexualOrientation? Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "straight":
                case "hetero":
                case "heterosexual":
                    return SexualOrientation.Heterosexual;
                case "bicurious":
                case "bi-curious":
                case "bi curious":
                case "curious":
                    return SexualOrientation.BiCurious;
                case "bi":
                case "bisexual":
                    return SexualOrientation.Bisexual;
                case "gay":
                case "lesbian":
                case "homo":
                case "homosexual":
                case "sapphic":
                    return SexualOrientation.Homosexual;
                case "pan":
                case "pansexual":
                    return SexualOrientation.Pansexual;
                case "ace":
                case "asexual":
                    return SexualOrientation.Asexual;
                default:
                    return null;
            }
        }
    }
}
