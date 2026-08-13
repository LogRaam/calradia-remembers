// Code written by Gabriel Mailhot, 12/08/2026.
// The player-facing wording, the report hint, and the diagnostic log block for an [ACTION] whose type is
// not a real mod verb (an invented "grant a fief" / "bless a marriage"). Extracted here, pure and tested,
// because it feeds three different channels and each must stay in step.
//
// The stake (player report): in a triumphant scene the old generic refusal "(That cannot be done here.
// Nothing happened.)" read as CONTRADICTING the story, because other real effects DID happen that turn (a
// +1 regard, a quest issued). The refusal itself is correct (an invented deed must never vanish silently),
// so the fix is wording plus a feedback funnel: the base message is scoped to the DEED, never claims the
// whole turn was inert, and the invented verb is always logged with its context so the author can add it
// later. Compose gates the in-chat report hint on the player's MCM toggle; FormatLogEntry builds the log
// block. No timestamp is stamped here: the caller prepends one.

#region

using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Builds the wording and the diagnostic record for an [ACTION] the bridge refused because its type is
    ///   not a real mod verb. Kept separate from a plain guard-refusal (see <see cref="ActionRefusalNote" />):
    ///   this one is a signal the model invented a verb the mod could support one day, so it is logged for the
    ///   author, and the base wording is scoped to the single deed rather than the whole turn.
    /// </summary>
    public static class UnknownActionNotice
    {
        /// <summary>
        ///   The in-world, out-of-character line shown to the player for an unknown-verb refusal. Scoped to the
        ///   DEED (never "nothing happened", which contradicted a scene where real effects also landed), and in
        ///   the tone of its siblings like "(No true harm befalls you here.)".
        /// </summary>
        public const string BaseMessage = "(That deed finds no purchase in the world here.)";

        /// <summary>
        ///   The sober, person-free report hint appended to <see cref="BaseMessage" /> only when the player has
        ///   turned reporting on. Names no one in-fiction: it points at the log and the mod page, nothing more.
        /// </summary>
        private const string ReportHint =
            " (An unsupported action was attempted and logged; see the mod page to report it.)";

        /// <summary>
        ///   The player-facing message for an unknown-verb refusal: <see cref="BaseMessage" /> alone when
        ///   reporting is off, or the base plus the report hint when it is on. The immersive base always shows.
        /// </summary>
        public static string Compose(bool reportEnabled)
            => reportEnabled ? BaseMessage + ReportHint : BaseMessage;

        /// <summary>
        ///   A readable multi-field block capturing the invented verb, its parameters, who attempted it, and the
        ///   surrounding lines, so the author can see the CONTEXT of what the model was trying to do and decide
        ///   whether to support it. Blank or null fields are omitted; a blank parameter value is skipped too.
        ///   Never throws. No timestamp is stamped here: the caller prepends one.
        /// </summary>
        public static string FormatLogEntry(
            string actionType,
            IReadOnlyDictionary<string, string> parameters,
            string npcLabel,
            string npcLine,
            string playerLine)
        {
            var sb = new StringBuilder();

            AppendField(sb, "Action type", actionType);

            if (parameters != null)
            {
                foreach (KeyValuePair<string, string> kv in parameters)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;

                    AppendField(sb, "  param " + kv.Key, kv.Value);
                }
            }

            AppendField(sb, "NPC", npcLabel);
            AppendField(sb, "NPC line", npcLine);
            AppendField(sb, "Player line", playerLine);

            return sb.ToString();
        }

        #region private

        private static void AppendField(StringBuilder sb, string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            sb.Append(label).Append(": ").Append(value.Trim()).Append('\n');
        }

        #endregion
    }
}
