// Code written by Gabriel Mailhot, 14/11/2026.
// The player's "report a mistake" (Gabriel, 14/11/2026, after reading AI Influence 7.0.5's player lessons: "J'aime le
// bouton ... On clique sur le bouton et on voit tout le contexte, les logs, la conversation et le joueur peut ajouter un
// texte explicatif du problème"; "L'idéal serait de réinjecter le rapport du joueur dans le log avec tout le contexte").
// Aporo's reports of the same day are why: his logs held another session, and the gold exchange he reported could not be
// read. This composes the report from what the mod holds: the player's words, the settings that shape a reply, the
// conversation, the model exchanges and the log. Keys are masked whatever the player chooses, and a player who would not
// share what was said (Hardcore, by default) gets a report without it.

#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Diagnostics
{
    /// <summary>One exchange with the model, as the report shows it.</summary>
    public sealed class ReportExchange
    {
        /// <summary>When it finished, "HH:mm:ss".</summary>
        public string Time { get; init; }

        /// <summary>The model that answered.</summary>
        public string Model { get; init; }

        /// <summary>Who it was about (the character), or null.</summary>
        public string Subject { get; init; }

        /// <summary>The system prompt sent.</summary>
        public string SystemPrompt { get; init; }

        /// <summary>The conversation messages sent, each "role: text".</summary>
        public IReadOnlyList<string> Messages { get; init; }

        /// <summary>The model's raw reply.</summary>
        public string Reply { get; init; }

        /// <summary>The error, when the call failed.</summary>
        public string Error { get; init; }
    }

    /// <summary>What a player report is made of.</summary>
    public sealed class PlayerReportFacts
    {
        /// <summary>What the player wrote about the problem.</summary>
        public string PlayerText { get; init; }

        /// <summary>The settings and versions that shape a reply, each "Name: value", in order.</summary>
        public IReadOnlyList<string> Header { get; init; }

        /// <summary>The conversation as the chat shows it, one line each ("Speaker: text").</summary>
        public IReadOnlyList<string> Transcript { get; init; }

        /// <summary>The latest exchanges with the model, oldest first.</summary>
        public IReadOnlyList<ReportExchange> Exchanges { get; init; }

        /// <summary>The log lines of this session, oldest first.</summary>
        public IReadOnlyList<string> LogLines { get; init; }

        /// <summary>The player chose not to share what was said: the conversation, the prompts and replies are left out.</summary>
        public bool RemoveConversationText { get; init; }

        /// <summary>
        ///   When set, the LOG section says this instead of listing lines: the copy written INTO the log points to the lines
        ///   already above it rather than writing them a second time (Gabriel, 14/11/2026: "il faudrait pas réinjecter le log au
        ///   complet").
        /// </summary>
        public string LogPointer { get; init; }
    }

    /// <summary>Composes a player report and masks anything that looks like a secret.</summary>
    public static class PlayerReportComposer
    {
        /// <summary>The line every report opens with in the log, so it is found among the rest.</summary>
        public const string BeginMarker = "==== CALRADIA REMEMBERS PLAYER REPORT ====";

        /// <summary>The line every report closes with.</summary>
        public const string EndMarker = "==== END OF PLAYER REPORT ====";

        /// <summary>What stands in for the conversation when the player chose not to share it.</summary>
        public const string Removed = "[removed by the player]";

        private static readonly Regex[] Secrets = {
            new Regex(@"sk-[A-Za-z0-9_\-]{12,}", RegexOptions.CultureInvariant),
            new Regex(@"xai-[A-Za-z0-9_\-]{12,}", RegexOptions.CultureInvariant),
            new Regex(@"AIza[0-9A-Za-z_\-]{20,}", RegexOptions.CultureInvariant),
            new Regex(@"gh[pousr]_[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant),
            new Regex(@"(?i)bearer\s+[A-Za-z0-9._\-]{8,}", RegexOptions.CultureInvariant),
            new Regex(@"(?i)(""?(?:api[_-]?key|apikey|authorization|x-api-key)""?\s*[:=]\s*)""?[^""\s,}]{6,}""?", RegexOptions.CultureInvariant)
        };

        private static readonly Regex Quoted = new Regex("\"[^\"\\r\\n]*\"", RegexOptions.CultureInvariant);

        private static readonly Regex Timestamped = new Regex(@"^\[\d\d:\d\d:\d\d\]", RegexOptions.CultureInvariant);

        /// <summary>
        ///   The whole report, plain text: the player's words, the header, the conversation, the exchanges and the log, between
        ///   <see cref="BeginMarker" /> and <see cref="EndMarker" />, with every secret masked. When the player chose not to share
        ///   what was said, the conversation, prompts and replies read <see cref="Removed" />, and of the log only timestamped
        ///   lines are kept, with anything in double quotes emptied.
        /// </summary>
        public static string Compose(PlayerReportFacts facts)
        {
            facts ??= new PlayerReportFacts();
            bool removed = facts.RemoveConversationText;
            var sb = new StringBuilder();

            sb.AppendLine(BeginMarker);
            sb.AppendLine();
            sb.AppendLine("WHAT THE PLAYER SAYS WENT WRONG");
            sb.AppendLine(string.IsNullOrWhiteSpace(facts.PlayerText) ? "(nothing written)" : facts.PlayerText.Trim());
            sb.AppendLine();

            sb.AppendLine("SETTINGS");
            foreach (string line in facts.Header ?? new List<string>()) sb.AppendLine(line);
            if (removed) sb.AppendLine("Conversation text: " + Removed);
            sb.AppendLine();

            sb.AppendLine("CONVERSATION");
            if (removed) sb.AppendLine(Removed);
            else foreach (string line in facts.Transcript ?? new List<string>()) sb.AppendLine(line);
            sb.AppendLine();

            List<ReportExchange> exchanges = (facts.Exchanges ?? new List<ReportExchange>()).Where(e => e != null).ToList();
            sb.AppendLine($"MODEL EXCHANGES ({exchanges.Count}, oldest first)");
            for (int i = 0; i < exchanges.Count; i++) AppendExchange(sb, i + 1, exchanges[i], removed);

            if (!string.IsNullOrWhiteSpace(facts.LogPointer))
            {
                sb.AppendLine("LOG");
                sb.AppendLine(facts.LogPointer);
                sb.AppendLine();
                sb.AppendLine(EndMarker);

                return Mask(sb.ToString());
            }

            List<string> log = (facts.LogLines ?? new List<string>())
                               .Where(l => !removed || (l != null && Timestamped.IsMatch(l)))
                               .Select(l => removed ? Quoted.Replace(l ?? "", "\"...\"") : l)
                               .ToList();
            sb.AppendLine($"LOG ({log.Count} lines of this session{(removed ? ", timestamped lines only, quotes emptied" : "")})");
            foreach (string line in log) sb.AppendLine(line);
            sb.AppendLine();
            sb.AppendLine(EndMarker);

            return Mask(sb.ToString());
        }

        /// <summary>The text with everything that looks like an API key, a token or an authorization header masked.</summary>
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string masked = text;
            foreach (Regex secret in Secrets)
                masked = secret.Replace(masked, m => m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1].Value + "[masked]" : "[masked]");

            return masked;
        }

        /// <summary>
        ///   The lines of a session's log from the first one stamped at or after <paramref name="since" /> (time of day), the
        ///   unstamped lines among them included (a prompt written out whole): the conversation's own part of the log. Empty
        ///   when no line is that late.
        /// </summary>
        public static List<string> LinesSince(IReadOnlyList<string> sessionLines, System.TimeSpan since)
        {
            var lines = (sessionLines ?? new List<string>()).ToList();
            int first = lines.FindIndex(l => l != null && Timestamped.IsMatch(l)
                                             && System.TimeSpan.TryParse(l.Substring(1, 8), System.Globalization.CultureInfo.InvariantCulture, out System.TimeSpan at)
                                             && at >= since);

            return first < 0 ? new List<string>() : lines.Skip(first).ToList();
        }

        /// <summary>The first <paramref name="maxChars" /> characters of a report, for the window, saying how much more the file holds.</summary>
        public static string Preview(string report, int maxChars)
        {
            if (string.IsNullOrEmpty(report) || report.Length <= maxChars) return report ?? "";

            return report.Substring(0, maxChars) + $"\n\n... ({report.Length - maxChars:N0} more characters in the report file)";
        }

        #region private

        private static void AppendExchange(StringBuilder sb, int number, ReportExchange e, bool removed)
        {
            sb.AppendLine($"--- Exchange {number}: {e.Time} | {e.Model} | {e.Subject ?? "no subject"}{(string.IsNullOrEmpty(e.Error) ? "" : " | ERROR: " + e.Error)}");
            if (removed)
            {
                sb.AppendLine($"(prompt of {(e.SystemPrompt ?? "").Length:N0} characters, {(e.Messages ?? new List<string>()).Count} messages, reply of {(e.Reply ?? "").Length:N0} characters: {Removed})");
                sb.AppendLine();

                return;
            }

            sb.AppendLine(">> SYSTEM PROMPT");
            sb.AppendLine(e.SystemPrompt ?? "");
            sb.AppendLine(">> MESSAGES");
            foreach (string m in e.Messages ?? new List<string>()) sb.AppendLine(m);
            sb.AppendLine(">> REPLY");
            sb.AppendLine(e.Reply ?? "");
            sb.AppendLine();
        }

        #endregion
    }
}
