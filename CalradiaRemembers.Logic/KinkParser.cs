// Code written by Gabriel Mailhot, 01/08/2026.
// Player request: kinks added at the table with cr.add_kink were lost the next time the authored lore
// reloaded (cr.reload_lore re-derives kinks from the NPC's current traits, discarding manual edits). This
// parser lets a player instead LOCK specific kinks for an NPC in character_overrides.json, mirroring
// OrientationParser exactly. A hand-typed kink name has to fail safe: a typo or an unrecognized word must be
// skipped quietly, never throw, so a bad line in a hand-edited file can't crash the campaign.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Parses author-written kink names (case-insensitive) to <see cref="Kink" /> values.</summary>
    public static class KinkParser
    {
        /// <summary>
        ///   Parses each name to a <see cref="Kink" />, trimming whitespace and matching case-insensitively
        ///   against the enum's own member names (the same names <c>cr.kinks</c> lists as valid). A name that
        ///   is blank or does not match a real <see cref="Kink" /> is skipped, not thrown. Duplicates are
        ///   removed, keeping the first occurrence's position. A null <paramref name="names" /> yields an
        ///   empty list, never null, so callers never need a second null check.
        /// </summary>
        public static List<Kink> Parse(IEnumerable<string> names)
        {
            var result = new List<Kink>();
            if (names == null) return result;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!Enum.TryParse(name.Trim(), true, out Kink kink)) continue;
                if (!result.Contains(kink)) result.Add(kink);
            }

            return result;
        }

        /// <summary>
        ///   The author-written names <see cref="Parse" /> could NOT match to a real <see cref="Kink" />, in the
        ///   order they were written. Parsing stays forgiving (a typo must never crash a campaign), but silence is
        ///   its own failure: an authored lock of nothing but typos quietly pins the NPC to ZERO kinks, and the
        ///   author has no way to tell that from a deliberate empty lock. Diagnostics report these so a hand-edited
        ///   file can be corrected. Blank entries are ignored, never reported as typos.
        /// </summary>
        public static List<string> Unrecognized(IEnumerable<string> names)
        {
            var bad = new List<string>();
            if (names == null) return bad;

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Enum.TryParse(name.Trim(), true, out Kink _)) continue;

                bad.Add(name.Trim());
            }

            return bad;
        }
    }
}
