// Code written by Gabriel Mailhot, 10/09/2026.
// Gabriel asked whether cr.help was still complete. It was not, and not by a little: 70 commands were described
// out of 259 registered, so 189 were invisible, including cr.testall and cr.test, which the two of us had run a
// dozen times that same evening. One of the 70 was dead: cr.compose had been renamed cr.letter_compose and the
// help still advertised the old name, so the single command a reader was most likely to copy verbatim was the one
// that would fail.
//
// Then, on being told the size of it: "les joueurs utilisent beaucoup les commandes consoles. Ce serait bien
// qu'elles soient bien documentees." So all 259 are described now, and this decides how those descriptions are
// answered without burying anyone.
//
// The cause of the drift was the shape, not carelessness. A hand-written list of every command in a project that
// gains commands weekly can only fall behind, and it falls behind in silence, because nothing ever compares it
// with anything. So what EXISTS is read from the registry, what each one DOES comes from a table that
// cr.test all refuses to let go incomplete, and the curated sections are an editorial layer on top: the twenty or
// so commands worth grouping and explaining at length, in the order a reader wants them.
//
// Gabriel's rule, 2026-09-09: a proven drift earns a MECHANISM, not a one-time correction.
//
// The other half is that 259 entries is a wall rather than help. So the curated page stays the default answer,
// and everything else is asked for: cr.help all to see the whole list, cr.help <text> to search it, and
// cr.help <command> to read one in full.

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
    ///   How cr.help answers, given what is actually registered and what each command is documented to do.
    ///   Pure: the caller reads the registry off the live assembly and hands both in.
    /// </summary>
    public static class ConsoleHelpPolicy
    {
        /// <summary>How much of a description a one-line listing shows before it stops at a sentence end.</summary>
        public const int ShortDescriptionChars = 120;

        /// <summary>Above this many matches a search answers with names alone, or it stops being readable.</summary>
        public const int CompactSearchThreshold = 12;

        /// <summary>
        ///   Registered commands with no entry in the description table. This is the omission that made
        ///   cr.testall itself invisible, and the self-test treats any of it as a failure: adding a command now
        ///   means writing its line, which is the only thing that keeps a help complete.
        /// </summary>
        public static List<string> Undocumented(IEnumerable<string> registered, IReadOnlyDictionary<string, ConsoleCommandDoc> docs)
            => Sorted(registered).Where(c => Doc(c, docs) == null).ToList();

        /// <summary>
        ///   Documented commands that NO LONGER EXIST. The error that actively misleads, rather than merely
        ///   omitting: a reader copies a name out of the help and it fails (cr.compose, 2026-09-10).
        /// </summary>
        public static List<string> Dead(IEnumerable<string> registered, IReadOnlyDictionary<string, ConsoleCommandDoc> docs)
        {
            if (docs == null) return new List<string>();

            var live = new HashSet<string>(Sorted(registered), StringComparer.OrdinalIgnoreCase);

            return docs.Keys.Where(c => !live.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        /// <summary>Commands the CURATED prose mentions that no longer exist. Same error, in the hand-written half.</summary>
        public static List<string> DeadInCuratedText(IEnumerable<string> registered, string curatedText)
        {
            var live = new HashSet<string>(Sorted(registered), StringComparer.OrdinalIgnoreCase);
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match m in Regex.Matches(curatedText ?? "", @"\bcr\.([a-zA-Z_0-9]+)"))
                named.Add(m.Groups[1].Value);

            return named.Where(c => !live.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        ///   The line that closes the curated help. Without it a reader has no way to know that what they just
        ///   read is a fraction of what exists, which is how 189 commands stayed unknown.
        /// </summary>
        public static string Footer(IEnumerable<string> registered)
            => $"{Sorted(registered).Count} commands in all. cr.help all lists them, "
             + "cr.help <text> searches by name, cr.help <command> explains one in full.";

        /// <summary>Every registered command, alphabetical, names only: the whole map on one screen.</summary>
        public static string RenderAll(IEnumerable<string> registered, IReadOnlyDictionary<string, ConsoleCommandDoc> docs)
        {
            List<string> all = Sorted(registered);
            if (all.Count == 0) return "No cr commands are registered, which should be impossible.";

            var sb = new StringBuilder();
            sb.AppendLine($"All {all.Count} cr commands. cr.help <name> explains any one of them.");

            foreach (string c in all)
            {
                ConsoleCommandDoc doc = Doc(c, docs);
                string usage = doc == null || doc.Usage.Length == 0 ? "" : " " + doc.Usage;
                sb.AppendLine($"  cr.{c}{usage}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        ///   One command in full: what it takes and everything written about it. This is the answer to
        ///   cr.help &lt;exact name&gt;, and the reason the table keeps the whole description rather than a summary.
        /// </summary>
        public static string Explain(string command, IReadOnlyDictionary<string, ConsoleCommandDoc> docs)
        {
            ConsoleCommandDoc doc = Doc(command, docs);
            if (doc == null) return "";

            string usage = doc.Usage.Length == 0 ? "" : " " + doc.Usage;

            return $"cr.{command.Trim()}{usage}\n  {doc.Text}";
        }

        /// <summary>
        ///   Commands whose name contains <paramref name="fragment" />, which is how anyone actually looks for one
        ///   they half remember. An exact name answers in full; a few matches get a line each; a long list gets
        ///   names alone, or the console stops being readable.
        /// </summary>
        public static string Search(IEnumerable<string> registered, IReadOnlyDictionary<string, ConsoleCommandDoc> docs, string fragment)
        {
            string needle = (fragment ?? "").Trim();
            if (needle.Length == 0) return RenderAll(registered, docs);

            List<string> all = Sorted(registered);

            List<string> hits = all.Where(c => c.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            string exact = all.FirstOrDefault(c => string.Equals(c, needle, StringComparison.OrdinalIgnoreCase));

            if (exact != null && Doc(exact, docs) != null)
            {
                string explained = Explain(exact, docs);

                // An exact hit used to END the answer, and that quietly cost the player the whole family:
                // `cr.help duel` explained cr.duel and never mentioned duel_challenge, duel_arm, duel_town,
                // duel_courtship or duel_here, so the one word most likely to be typed was the one that hid
                // five commands (cr.testall, 2026-09-10). A name that is also the stem of a family now answers
                // as both: the command in full, then its relatives by name, because a player who typed the
                // stem is exactly the player who does not yet know what the family contains.
                List<string> family = hits.Where(c => !string.Equals(c, exact, StringComparison.OrdinalIgnoreCase))
                                          .ToList();
                if (family.Count == 0) return explained;

                var withFamily = new StringBuilder(explained);
                withFamily.AppendLine();
                withFamily.AppendLine();
                withFamily.AppendLine($"{family.Count} more command(s) whose name contains '{needle}':");
                foreach (string c in family) withFamily.AppendLine("  " + Line(c, docs, family.Count > CompactSearchThreshold));
                withFamily.Append("cr.help <name> reads any of them in full.");

                return withFamily.ToString();
            }

            if (hits.Count == 0)
                return $"No cr command's name contains '{needle}'. cr.help all lists every one.";

            var sb = new StringBuilder();
            sb.AppendLine($"{hits.Count} command(s) matching '{needle}':");

            bool compact = hits.Count > CompactSearchThreshold;

            foreach (string c in hits) sb.AppendLine("  " + Line(c, docs, compact));

            if (compact) sb.AppendLine("Narrow the search, or cr.help <name>, to read what they do.");

            return sb.ToString().TrimEnd();
        }

        /// <summary>One command as a search result: its name and usage, plus a first sentence unless the list is long.</summary>
        private static string Line(string command, IReadOnlyDictionary<string, ConsoleCommandDoc> docs, bool compact)
        {
            ConsoleCommandDoc doc = Doc(command, docs);
            string usage = doc == null || doc.Usage.Length == 0 ? "" : " " + doc.Usage;

            return compact || doc == null ? $"cr.{command}{usage}" : $"cr.{command}{usage}\n      {Shorten(doc.Text)}";
        }

        /// <summary>
        ///   The first sentence of a description, or a clean truncation when the first sentence runs long. Cuts
        ///   at a word, never mid-word, so a listing never ends in half a name.
        /// </summary>
        public static string Shorten(string text)
        {
            string t = (text ?? "").Trim();
            if (t.Length == 0) return "";

            int stop = t.IndexOf(". ", StringComparison.Ordinal);
            if (stop > 0 && stop + 1 <= ShortDescriptionChars) return t.Substring(0, stop + 1);
            if (t.Length <= ShortDescriptionChars) return t;

            int cut = t.LastIndexOf(' ', Math.Min(ShortDescriptionChars, t.Length - 1));
            if (cut <= 0) cut = Math.Min(ShortDescriptionChars, t.Length);

            return t.Substring(0, cut).TrimEnd(',', ';', ':', ' ') + "...";
        }

        private static ConsoleCommandDoc Doc(string command, IReadOnlyDictionary<string, ConsoleCommandDoc> docs)
        {
            if (docs == null || string.IsNullOrWhiteSpace(command)) return null;

            return docs.TryGetValue(command.Trim(), out ConsoleCommandDoc doc) && doc != null && doc.Text.Length > 0
                ? doc
                : null;
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
