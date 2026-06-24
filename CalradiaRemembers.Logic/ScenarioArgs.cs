// Code written by Gabriel Mailhot, 24/06/2026.
// Small pure helpers for reading a consequence/guard payload — e.g. CHANGE_RELATION:Rhagaea:+5 carries the
// target and a signed delta. Kept here (not in the engine executor) so the parse rules are pinned by tests
// and read as documentation of the keyword grammar.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Parsing helpers for the scripted-scenarios keyword payloads.</summary>
    public static class ScenarioArgs
    {
        /// <summary>
        ///   Splits a <c>target:delta</c> payload (e.g. <c>Rhagaea:+5</c>) into its target and a signed
        ///   integer. Splits on the LAST colon, so a target may itself contain colons. Returns false when
        ///   there is no colon, the target is empty, or the value is not an integer.
        /// </summary>
        public static bool TryParseTargetDelta(string payload, out string target, out int delta)
        {
            target = null;
            delta = 0;
            if (string.IsNullOrWhiteSpace(payload)) return false;

            int colon = payload.LastIndexOf(':');
            if (colon <= 0 || colon == payload.Length - 1) return false;

            string head = payload.Substring(0, colon).Trim();
            string tail = payload.Substring(colon + 1).Trim();
            if (head.Length == 0) return false;
            if (!TryParseSignedInt(tail, out delta)) return false;

            target = head;
            return true;
        }

        /// <summary>
        ///   Splits a <c>target:rest</c> payload (e.g. <c>Rhagaea:she wed the player</c>) into its target and
        ///   the remaining free text. Splits on the FIRST colon, so the rest may itself contain colons.
        ///   Returns false when there is no colon, the target is empty, or the rest is empty.
        /// </summary>
        public static bool TryParseTargetRest(string payload, out string target, out string rest)
        {
            target = null;
            rest = null;
            if (string.IsNullOrWhiteSpace(payload)) return false;

            int colon = payload.IndexOf(':');
            if (colon <= 0) return false;

            string head = payload.Substring(0, colon).Trim();
            string tail = payload.Substring(colon + 1).Trim();
            if (head.Length == 0 || tail.Length == 0) return false;

            target = head;
            rest = tail;
            return true;
        }

        /// <summary>Parses an integer that may carry a leading <c>+</c> (e.g. <c>+5</c>, <c>-3</c>, <c>0</c>).</summary>
        public static bool TryParseSignedInt(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;

            string t = s.Trim();
            if (t.StartsWith("+", StringComparison.Ordinal)) t = t.Substring(1);

            return int.TryParse(t, out value);
        }
    }
}
