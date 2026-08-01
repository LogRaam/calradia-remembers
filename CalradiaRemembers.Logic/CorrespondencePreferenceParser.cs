// Code written by Gabriel Mailhot, 01/08/2026.
// A2 companion to CorrespondenceInterestPolicy: turns the terse control line an LLM may emit when a reply
// carries a stated correspondence preference ("write less often", "I don't want to hear about this again")
// into a typed CorrespondencePreference. Modelled on LetterReplyDecisionParser's tolerance: the marker is
// optional (most replies carry no preference at all, which must parse as None, not an error), and a marker
// present with a missing or garbled field degrades to a safe default rather than losing the whole preference,
// because a weak model that gets the shape half right should still move the dial in the direction it clearly
// intended.

#region

using System;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Parses the optional correspondence-preference control line an LLM may append to a reply, into a
    ///   typed <see cref="CorrespondencePreference" />. Tolerant by construction: an absent marker is None
    ///   (the ordinary case), and a present marker with a missing or garbled field falls back to a named
    ///   default for that field rather than discarding the whole preference. Never throws.
    /// </summary>
    public static class CorrespondencePreferenceParser
    {
        /// <summary>The marker that introduces a stated correspondence preference. Matched case-insensitively.</summary>
        public const string Marker = "[CORRESPONDENCE_PREFERENCE]";

        /// <summary>
        ///   Parses <paramref name="raw" />. A null, whitespace, or marker-less input yields
        ///   <see cref="CorrespondencePreference.None" />: most replies state no preference at all, and that
        ///   must be silent, not an error. When the marker is present, <c>scope=</c>, <c>direction=</c> and
        ///   <c>strength=</c> tokens are read case-insensitively, in any order, whether on the same line as
        ///   the marker or split across following lines. A missing or unrecognised <c>scope</c> (or an
        ///   explicit <c>none</c>) discards the whole preference back to <see cref="CorrespondencePreference.None" />;
        ///   a missing or garbled <c>direction</c> defaults to <see cref="PreferenceDirection.Less" />; a
        ///   missing or garbled <c>strength</c> defaults to <see cref="PreferenceStrength.Mild" />.
        /// </summary>
        public static CorrespondencePreference Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return CorrespondencePreference.None;

            int markerIndex = raw.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0) return CorrespondencePreference.None;

            string tail = raw.Substring(markerIndex + Marker.Length);

            PreferenceScope scope = ParseScope(ExtractToken(tail, "scope"));
            if (scope == PreferenceScope.None) return CorrespondencePreference.None;

            PreferenceDirection direction = ParseDirection(ExtractToken(tail, "direction"));
            PreferenceStrength strength = ParseStrength(ExtractToken(tail, "strength"));

            return new CorrespondencePreference(scope, direction, strength);
        }

        private static string ExtractToken(string text, string key)
        {
            Match match = Regex.Match(text, key + @"\s*=\s*(\w+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static PreferenceScope ParseScope(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return PreferenceScope.None;

            if (token.Equals("sender", StringComparison.OrdinalIgnoreCase)) return PreferenceScope.Sender;
            if (token.Equals("subject", StringComparison.OrdinalIgnoreCase)) return PreferenceScope.Subject;
            if (token.Equals("all", StringComparison.OrdinalIgnoreCase)) return PreferenceScope.All;

            return PreferenceScope.None; // "none", or anything unrecognised, discards the whole preference
        }

        private static PreferenceDirection ParseDirection(string token)
        {
            return token != null && token.Equals("more", StringComparison.OrdinalIgnoreCase)
                ? PreferenceDirection.More
                : PreferenceDirection.Less; // missing or garbled defaults to Less
        }

        private static PreferenceStrength ParseStrength(string token)
        {
            return token != null && token.Equals("strong", StringComparison.OrdinalIgnoreCase)
                ? PreferenceStrength.Strong
                : PreferenceStrength.Mild; // missing or garbled defaults to Mild
        }
    }
}
