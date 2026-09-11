// Code written by Gabriel Mailhot, 08/07/2026.
// Viable-quest referential (ratified 2026-07-08): the root cause of "the NPC offered a task but no quest was
// registered" is that the LLM was taught 17 quest types in the ABSTRACT, blind to the world, so it invented
// targets the factory could not ground. The fix is prevention: before the conversation, the host SCANS the world
// for the tasks this giver can truly hand out right now (a real infested hideout, a real enemy lord, a real
// war), and the prompt teaches ONLY that grounded menu, targets named. Registration then succeeds by
// construction. This type is the pure, unit-tested formatting core; the world scan is host glue.

#region

using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One task a giver can truly offer right now: its type token, its REAL target, and a short cue.</summary>
    public sealed class QuestMenuEntry
    {
        /// <summary>The exact type token the LLM must emit (e.g. "bandit_hideout").</summary>
        private readonly string _typeToken;

        public string TypeToken
        {
            get => _typeToken;
            init => _typeToken = value ?? "";
        }

        /// <summary>The target field this type needs ("target_settlement" / "target_hero" / "target_faction"), or null when none.</summary>
        public string TargetField { get; init; }

        /// <summary>The REAL, resolvable target name the LLM must use verbatim, or null when the type needs none.</summary>
        public string TargetName { get; init; }

        /// <summary>A short in-fiction cue for why this task exists (e.g. "a lair infests the hills nearby").</summary>
        private readonly string _cue;

        public string Cue
        {
            get => _cue;
            init => _cue = value ?? "";
        }
    }

    /// <summary>
    ///   Formats the grounded quest menu into compact prompt lines. One line per entry:
    ///   "- type (target_field: Name): cue". Returns "" when no entry is viable, so the caller can
    ///   fall back to teaching nothing (a giver with truly nothing to offer gives no task).
    /// </summary>
    public static class QuestMenuComposer
    {
        /// <summary>TUNE: hard cap on menu lines, so the grounded menu never bloats the prompt.</summary>
        public const int MaxEntries = 8;

        public static string Compose(IReadOnlyList<QuestMenuEntry> entries)
        {
            if (entries == null || entries.Count == 0) return "";

            var sb = new StringBuilder();
            int count = 0;
            foreach (QuestMenuEntry e in entries)
            {
                if (e == null || e.TypeToken.Length == 0) continue;
                if (count == MaxEntries) break;

                sb.Append("- ").Append(e.TypeToken);
                if (!string.IsNullOrWhiteSpace(e.TargetField) && !string.IsNullOrWhiteSpace(e.TargetName))
                    sb.Append(" (").Append(e.TargetField).Append(": ").Append(e.TargetName).Append(')');
                if (e.Cue.Length > 0) sb.Append(": ").Append(e.Cue);
                sb.AppendLine();
                count++;
            }

            return sb.ToString().TrimEnd();
        }
    }
}
