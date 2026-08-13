// Code written by Gabriel Mailhot, 13/08/2026.
// Parses the "Extraction Timing" MCM dropdown string into the ExtractionTiming enum. Pure and unit-tested so
// the label strings and the enum can never drift: the MCM dropdown is built from the same label constants this
// parser matches on, and any unexpected value degrades to Synchronous (the recommended, in-turn behaviour)
// rather than a mode that shows the reply first and applies its tags later.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>When the PROSE+EXTRACT extractor's tags are applied, relative to the reply the player already saw.</summary>
    public enum ExtractionTiming
    {
        /// <summary>Run the extractor and apply its tags BEFORE control returns to the player (recommended).</summary>
        Synchronous,

        /// <summary>Show the reply at once and apply the extracted tags a moment later, off the turn.</summary>
        Asynchronous
    }

    /// <summary>Maps the "Extraction Timing" MCM dropdown value to <see cref="ExtractionTiming" />.</summary>
    public static class ExtractionTimingPolicy
    {
        /// <summary>The dropdown label for applying the extracted tags in-turn (the default).</summary>
        public const string SynchronousLabel = "Synchronous";

        /// <summary>The dropdown label for showing the reply first and applying the tags a moment later.</summary>
        public const string AsynchronousLabel = "Asynchronous";

        /// <summary>
        ///   The timing a dropdown value resolves to. Anything blank or unrecognised degrades to
        ///   <see cref="ExtractionTiming.Synchronous" />, so a stale or hand-edited config keeps the safe,
        ///   in-turn behaviour where flow-control tags (end_conversation) still land before control returns.
        /// </summary>
        public static ExtractionTiming Parse(string dropdownValue)
        {
            if (string.IsNullOrWhiteSpace(dropdownValue)) return ExtractionTiming.Synchronous;

            string v = dropdownValue.Trim();

            if (string.Equals(v, AsynchronousLabel, StringComparison.OrdinalIgnoreCase)) return ExtractionTiming.Asynchronous;

            return ExtractionTiming.Synchronous;
        }
    }
}
