// Code written by Gabriel Mailhot, 23/08/2026.
// Pure selection of a witness's compact recall for a group scene: from their (already compression-folded) event
// summaries, keep at most `cap`, and when there are more, always keep the OLDEST kept line (the folded summary,
// or the first meeting) plus the most RECENT, so no salient past is silently dropped while the group prompt
// stays bounded. Kept engine-agnostic (a list of strings in, a joined string out) so the rule is documented by
// its tests rather than read out of the mod's witness-building code.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Composes a witness's short recall line from their event summaries, oldest-first.</summary>
    public static class WitnessMemoryDigest
    {
        /// <summary>
        ///   At most <paramref name="cap" /> summaries, oldest-first, joined with "; ". Blank summaries are
        ///   ignored. When more than the cap remain, the FIRST kept summary (the folded compression summary, or
        ///   the first meeting) is always retained plus the most recent, so the past is never lost. Returns null
        ///   when <paramref name="summaries" /> is null, empty of real content, or <paramref name="cap" /> is not
        ///   positive.
        /// </summary>
        public static string Compose(IReadOnlyList<string> summaries, int cap)
        {
            if (summaries == null || cap <= 0) return null;

            var clean = new List<string>();
            foreach (string s in summaries)
                if (!string.IsNullOrWhiteSpace(s))
                    clean.Add(s.Trim());

            if (clean.Count == 0) return null;

            IEnumerable<string> chosen = clean.Count <= cap
                ? clean
                : new[] {clean[0]}.Concat(clean.Skip(clean.Count - (cap - 1)));

            string joined = string.Join("; ", chosen);

            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
    }
}
