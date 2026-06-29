// Code written by Gabriel Mailhot, 28/06/2026.
// Pure: decides whether a player line is asking where to FIND a companion to recruit, so the host
// only gathers and injects the real "wanderers for hire" facts when it actually matters. Kept here,
// behind tests, because a too-greedy match wastes tokens on every line and a too-narrow one lets the
// NPC fall back to inventing a companion (the "Oros" bug).

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Detects a "where can I find / hire a companion?" intent in a free-text player message.
    ///   Substring match on a small curated vocabulary; the host uses it as a gate before listing
    ///   real wanderers. False positives only cost a small grounded prompt block, so the set leans
    ///   toward catching the ask rather than perfect precision.
    /// </summary>
    public static class RecruitIntentDetector
    {
        /// <summary>The words that mark a recruiting question, matched case-insensitively as substrings.</summary>
        private static readonly string[] Keywords =
        {
            "companion",   // covers companions
            "wanderer",    // covers wanderers
            "sellsword",   // covers sellswords
            "mercenar",    // mercenary / mercenaries
            "recruit",     // recruit / recruiting / a recruit
            "for hire",
            "hire a",
            "hire on",
            "hire someone",
            "someone to hire",
            "join my party",
            "join my band",
            "fight for me"
        };

        /// <summary>
        ///   True when <paramref name="message" /> reads as a question about where to find or hire a
        ///   companion. Empty or whitespace input is never a match.
        /// </summary>
        public static bool Detect(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            string text = message.ToLowerInvariant();
            foreach (string keyword in Keywords)
                if (text.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                    return true;

            return false;
        }
    }
}
