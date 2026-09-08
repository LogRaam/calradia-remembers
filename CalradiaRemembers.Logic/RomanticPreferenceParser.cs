// Code written by Gabriel Mailhot, 07/09/2026.
// Player request (Nexus, 2026-09-07): "Would it be possible to set whether a character is monogamous or
// polyamorous via character_overrides.json?" It was not: preferences were rolled from a pool the author could
// only nudge by lying about a trait, and after the same player's other point was ruled on (polyamory hangs on
// CULTURE, never on honour) even that crooked workaround stopped working, leaving no way at all to write one
// polyamorous character into a strict culture. This parser is that way.
//
// Mirrors KinkParser exactly, and for the same reason: a hand-typed name has to fail safe. A typo must be
// skipped quietly and reported afterwards, never thrown, so a bad line in a hand-edited file cannot crash a
// campaign, and never swallowed in silence either, or the author cannot tell a typo from a deliberate choice.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Parses author-written romantic-preference names (case-insensitive) to <see cref="RomanticPreference" /> values.</summary>
    public static class RomanticPreferenceParser
    {
        /// <summary>
        ///   Parses each name to a <see cref="RomanticPreference" />, trimming whitespace and matching
        ///   case-insensitively against the enum's own member names. A name that is blank, unrecognized, or the
        ///   placeholder <see cref="RomanticPreference.None" /> is skipped, not thrown. Duplicates are removed,
        ///   keeping the first occurrence's position. A null <paramref name="names" /> yields an empty list,
        ///   never null, so callers never need a second null check.
        /// </summary>
        public static List<RomanticPreference> Parse(IEnumerable<string> names)
        {
            var result = new List<RomanticPreference>();
            if (names == null) return result;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!Enum.TryParse(name.Trim(), true, out RomanticPreference preference)) continue;
                if (preference == RomanticPreference.None) continue; // a placeholder, not something to be
                if (!result.Contains(preference)) result.Add(preference);
            }

            return result;
        }

        /// <summary>
        ///   The author-written names <see cref="Parse" /> could NOT use, in the order they were written.
        ///   Parsing stays forgiving, but silence is its own failure: a lock made of nothing but typos pins the
        ///   NPC to ZERO preferences, and the author has no way to tell that from a deliberate empty lock.
        ///   <see cref="RomanticPreference.None" /> is reported too: it parses, so it is not a typo, but it is a
        ///   placeholder and writing it does nothing, which is exactly the kind of quiet nothing worth naming.
        ///   Blank entries are ignored, never reported.
        /// </summary>
        public static List<string> Unrecognized(IEnumerable<string> names)
        {
            var bad = new List<string>();
            if (names == null) return bad;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Enum.TryParse(name.Trim(), true, out RomanticPreference preference) && preference != RomanticPreference.None) continue;

                bad.Add(name.Trim());
            }

            return bad;
        }
    }
}
