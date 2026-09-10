// Code written by Gabriel Mailhot, 10/09/2026.
// Gabriel asked whether cr.help was still complete. It was not, and not by a little: 70 commands were described
// out of 259 registered, so 189 were invisible, including cr.testall and cr.test, which the two of us had run a
// dozen times that same evening. One of the 70 was dead: cr.compose had been renamed cr.letter_compose and the
// help still advertised the old name, so the single command a reader was most likely to copy verbatim was the one
// that would fail.
//
// The cause is not carelessness, it is the shape. A hand-written list of every command in a project that gains
// commands weekly can only drift, and it drifts silently, because nothing ever compares it with anything. So the
// list stops being authored: the REGISTRY is the source of truth (every [CommandLineArgumentFunction] the
// assembly actually carries), the curated prose is an OVERLAY on top of it, and a command with no prose still
// appears rather than vanishing.
//
// Gabriel's rule, 2026-09-09: a proven drift earns a MECHANISM, not a one-time correction. The mechanism is that
// cr.help can no longer omit anything, by construction, and cr.testall's console_help_complete fails the moment a
// described command stops existing.
//
// The other half is that 259 lines is not help, it is a wall. So the curated sections stay the default answer,
// and the full list and the search are asked for.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   How cr.help answers, given what is actually registered and what the curated text happens to describe.
    ///   Pure: the caller reads the registry off the live assembly and hands it in.
    /// </summary>
    public static class ConsoleHelpPolicy
    {
        /// <summary>Above this many matches a search is answered as names only, without the prose.</summary>
        public const int CompactSearchThreshold = 12;

        /// <summary>
        ///   Every command the curated prose mentions, as bare names ("testall", not "cr.testall"). Parsed from
        ///   the text rather than kept in a second list beside it, because a second list is one more thing that
        ///   can disagree with the first, which is the whole failure being fixed here.
        /// </summary>
        public static HashSet<string> DescribedIn(string curatedText)
        {
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(curatedText)) return found;

            foreach (Match m in Regex.Matches(curatedText, @"\bcr\.([a-zA-Z_0-9]+)"))
                found.Add(m.Groups[1].Value);

            return found;
        }

        /// <summary>
        ///   Registered commands the curated prose says nothing about. Not an error: most of these are dev
        ///   probes nobody needs a paragraph for. They must simply still be REACHABLE, which is what
        ///   <see cref="RenderAll" /> is for.
        /// </summary>
        public static List<string> Undescribed(IEnumerable<string> registered, string curatedText)
        {
            HashSet<string> described = DescribedIn(curatedText);

            return Sorted(registered).Where(c => !described.Contains(c)).ToList();
        }

        /// <summary>
        ///   Commands the curated prose describes that NO LONGER EXIST. This one IS an error, and the reason the
        ///   self-test exists: a reader copies a name from the help, and a renamed command turns the help itself
        ///   into the thing that misleads them (cr.compose, 2026-09-10).
        /// </summary>
        public static List<string> Dead(IEnumerable<string> registered, string curatedText)
        {
            var live = new HashSet<string>(Sorted(registered), StringComparer.OrdinalIgnoreCase);

            return DescribedIn(curatedText).Where(c => !live.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        ///   The line that closes the curated help: how much of the whole it covers, and how to reach the rest.
        ///   Without it a reader has no way to know that what they just read is a quarter of what exists.
        /// </summary>
        public static string Footer(IEnumerable<string> registered, string curatedText)
        {
            List<string> all = Sorted(registered);
            int described = all.Count - Undescribed(all, curatedText).Count;

            return $"{described} of {all.Count} commands are described above. "
                 + "cr.help all lists every one of them; cr.help <text> searches by name.";
        }

        /// <summary>Every registered command, alphabetical, marking the ones the curated help explains.</summary>
        public static string RenderAll(IEnumerable<string> registered, string curatedText)
        {
            List<string> all = Sorted(registered);
            if (all.Count == 0) return "No cr commands are registered, which should be impossible.";

            HashSet<string> described = DescribedIn(curatedText);
            var sb = new StringBuilder();
            sb.AppendLine($"All {all.Count} cr commands. A dot marks one that cr.help describes in full.");

            foreach (string c in all)
                sb.AppendLine($"  {(described.Contains(c) ? "." : " ")} cr.{c}");

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        ///   Commands whose name contains <paramref name="fragment" />, which is how anyone actually looks for a
        ///   command they half remember. A long result collapses to names only so the console stays readable.
        /// </summary>
        public static string Search(IEnumerable<string> registered, string curatedText, string fragment)
        {
            string needle = (fragment ?? "").Trim();
            if (needle.Length == 0) return RenderAll(registered, curatedText);

            List<string> hits = Sorted(registered)
                                .Where(c => c.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                                .ToList();

            if (hits.Count == 0)
                return $"No cr command's name contains '{needle}'. cr.help all lists every one.";

            var sb = new StringBuilder();
            sb.AppendLine($"{hits.Count} command(s) matching '{needle}':");

            foreach (string c in hits) sb.AppendLine($"  cr.{c}");

            if (hits.Count > CompactSearchThreshold)
                sb.AppendLine("Narrow the search for a shorter list.");

            return sb.ToString().TrimEnd();
        }

        private static List<string> Sorted(IEnumerable<string> registered)
            => (registered ?? Enumerable.Empty<string>())
               .Where(c => !string.IsNullOrWhiteSpace(c))
               .Select(c => c.Trim())
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .OrderBy(c => c, StringComparer.Ordinal)
               .ToList();
    }
}
