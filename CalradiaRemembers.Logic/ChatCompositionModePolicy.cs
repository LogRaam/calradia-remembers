// Code written by Gabriel Mailhot, 13/08/2026.
// Parses the "Chat Composition" MCM dropdown string into the ChatCompositionMode enum. Pure and unit-tested so
// the label strings and the enum can never drift: the MCM dropdown is built from the same label constants this
// parser matches on, and any unexpected value degrades to Integrated (today's single-call behaviour) rather
// than silently switching a player onto the unfinished prose + interpreter path.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Maps the "Chat Composition" MCM dropdown value to <see cref="ChatCompositionMode" />.</summary>
    public static class ChatCompositionModePolicy
    {
        /// <summary>The dropdown label for the single-call, integrated path (the default).</summary>
        public const string IntegratedLabel = "Integrated";

        /// <summary>The dropdown label for the two-call prose-then-interpreter path.</summary>
        public const string ProseInterpretLabel = "Prose + Interpreter";

        /// <summary>
        ///   The composition mode a dropdown value resolves to. Anything blank or unrecognised degrades to
        ///   <see cref="ChatCompositionMode.Integrated" /> so a stale or hand-edited config never lands a player
        ///   on the unfinished prose + interpreter path by accident.
        /// </summary>
        public static ChatCompositionMode Parse(string dropdownValue)
        {
            if (string.IsNullOrWhiteSpace(dropdownValue)) return ChatCompositionMode.Integrated;

            string v = dropdownValue.Trim();

            if (string.Equals(v, ProseInterpretLabel, StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.ProseInterpret;
            if (string.Equals(v, "ProseInterpret", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.ProseInterpret;

            return ChatCompositionMode.Integrated;
        }
    }
}
