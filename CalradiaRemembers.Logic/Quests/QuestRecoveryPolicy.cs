// Code written by Gabriel Mailhot, 02/09/2026.
// Backs the cr.quest_void recovery command: a player reported a quest that became impossible (a capture-a-lord
// task that outlived a peace treaty) and could not be dropped in conversation. cr.quests_all lists every
// outstanding quest with a number; this policy turns the number the player then types into a validated 0-based
// index into that same list, with a clear message for every wrong input (empty, non-numeric, out of range,
// nothing to void). Kept pure so the parsing and bounds are tested; the command only enumerates the quests and
// performs the neutral void.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>Resolves the 1-based quest number a player typed into a 0-based index, or an error to show.</summary>
    public static class QuestRecoveryPolicy
    {
        /// <summary>The outcome of resolving a typed quest number: a valid 0-based <see cref="Index" /> or an <see cref="Error" />.</summary>
        public struct Selection
        {
            /// <summary>The 0-based index into the listed quests; valid only when non-negative (see <see cref="Ok" />).</summary>
            public int Index;

            /// <summary>The message to show when the selection is invalid; null on success.</summary>
            public string Error;

            /// <summary>True when a real quest was selected.</summary>
            public bool Ok => Index >= 0;
        }

        /// <summary>
        ///   Resolves the number the player typed (1-based, as shown by cr.quests_all) against the count of
        ///   currently outstanding quests. Success carries the 0-based index; every failure carries a message
        ///   that names the fix, so the player is never left guessing.
        /// </summary>
        public static Selection Resolve(string arg, int count)
        {
            if (count <= 0)
                return new Selection {Index = -1, Error = "No outstanding quests to void."};
            if (string.IsNullOrWhiteSpace(arg))
                return new Selection {Index = -1, Error = "Pass the quest number from cr.quests_all (for example: cr.quest_void 2)."};
            if (!int.TryParse(arg.Trim(), out int oneBased))
                return new Selection {Index = -1, Error = $"'{arg.Trim()}' is not a number. Use the number shown by cr.quests_all."};
            if (oneBased < 1 || oneBased > count)
                return new Selection {Index = -1, Error = $"No quest #{oneBased}. There are {count} outstanding (see cr.quests_all)."};

            return new Selection {Index = oneBased - 1, Error = null};
        }
    }
}
