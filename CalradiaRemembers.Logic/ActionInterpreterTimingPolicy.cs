// Code written by Gabriel Mailhot, 13/08/2026.
// Parses the "Actions Interpreter Timing" MCM dropdown string into the ActionInterpreterTiming enum. Pure and
// unit-tested so the label strings and the enum can never drift: the MCM dropdown is built from the same label
// constants this parser matches on, and any unexpected value degrades to Synchronous (the recommended, in-turn
// behaviour) rather than a mode that shows the reply first and applies its tags later.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>When the PROSE + INTERPRETER action interpreter's tags are applied, relative to the reply the player already saw.</summary>
    public enum ActionInterpreterTiming
    {
        /// <summary>Run the action interpreter and apply its tags BEFORE control returns to the player (recommended).</summary>
        Synchronous,

        /// <summary>Show the reply at once and apply the interpreted tags a moment later, off the turn.</summary>
        Asynchronous
    }

    /// <summary>Maps the "Actions Interpreter Timing" MCM dropdown value to <see cref="ActionInterpreterTiming" />.</summary>
    public static class ActionInterpreterTimingPolicy
    {
        /// <summary>The dropdown label for applying the interpreted tags in-turn (the default).</summary>
        public const string SynchronousLabel = "Synchronous";

        /// <summary>The dropdown label for showing the reply first and applying the tags a moment later.</summary>
        public const string AsynchronousLabel = "Asynchronous";

        /// <summary>
        ///   The timing a dropdown value resolves to. Anything blank or unrecognised degrades to
        ///   <see cref="ActionInterpreterTiming.Synchronous" />, so a stale or hand-edited config keeps the safe,
        ///   in-turn behaviour where flow-control tags (end_conversation) still land before control returns.
        /// </summary>
        public static ActionInterpreterTiming Parse(string dropdownValue)
        {
            if (string.IsNullOrWhiteSpace(dropdownValue)) return ActionInterpreterTiming.Synchronous;

            string v = dropdownValue.Trim();

            if (string.Equals(v, AsynchronousLabel, StringComparison.OrdinalIgnoreCase)) return ActionInterpreterTiming.Asynchronous;

            return ActionInterpreterTiming.Synchronous;
        }
    }
}
