// Code written by Gabriel Mailhot, 05/08/2026.
// v1.34.2 resilience: sway_opinion used to read its stance as "against = anything that is not the literal word
// 'for'", so a malformed, blank, or hallucinated stance token silently became the HARMFUL direction (a relation
// hit, a Rivalry rumour, a grudge exposure). The default must never be harm: this strict whitelist parser tells
// "for" and "against" apart and reports anything else as Unrecognized, so the verb can REFUSE rather than wound a
// third party the model never clearly meant to turn the NPC against.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The stance a sway_opinion action asked for, once its raw token is read strictly.</summary>
    public enum SwayStance
    {
        /// <summary>The token clearly named the "for" direction (warm the NPC toward the target).</summary>
        For,

        /// <summary>The token clearly named the "against" direction (turn the NPC against the target).</summary>
        Against,

        /// <summary>The token was blank, absent, or anything other than a clear "for"/"against": refuse, never default to harm.</summary>
        Unrecognized
    }

    /// <summary>
    ///   Reads a sway_opinion stance token against a strict whitelist. Only an exact (trimmed, case-insensitive)
    ///   "for" or "against" is accepted; every other value, blank or garbage alike, is <see cref="SwayStance.Unrecognized" />
    ///   so the caller refuses rather than silently applying the damaging "against" direction.
    /// </summary>
    public static class SwayStanceParser
    {
        public static SwayStance Parse(string raw)
        {
            string token = raw?.Trim();

            if (string.Equals(token, "for", StringComparison.OrdinalIgnoreCase)) return SwayStance.For;
            if (string.Equals(token, "against", StringComparison.OrdinalIgnoreCase)) return SwayStance.Against;

            return SwayStance.Unrecognized;
        }
    }
}
