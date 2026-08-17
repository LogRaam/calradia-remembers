// Code written by Gabriel Mailhot, 16/08/2026.
// Parses the "Chat Composition" MCM dropdown string into the ChatCompositionMode enum. Pure and unit-tested so the
// label strings and the enum can never drift: the MCM dropdown is built from the same label constants this parser
// matches on. ENHANCED is the default (richest experience), so anything blank or unrecognised degrades to Enhanced.
// Migration: the legacy labels "Integrated" and "Prose + Interpreter" map to Standard and Enhanced respectively, so
// a config from the old two-mode era keeps its intent.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Maps the "Chat Composition" MCM dropdown value to <see cref="ChatCompositionMode" />.</summary>
    public static class ChatCompositionModePolicy
    {
        /// <summary>The dropdown label for the cheap single integrated call, content-routed (PROSE for chat, NSFW for captivity).</summary>
        public const string StandardLabel = "Standard";

        /// <summary>The dropdown label for the full PROSE + INTERPRET pipeline with the NSFW fallback (the default).</summary>
        public const string EnhancedLabel = "Enhanced";

        /// <summary>
        ///   The composition mode a dropdown value resolves to. ENHANCED is the default, so anything blank or
        ///   unrecognised degrades to it. The legacy labels "Integrated" -> Standard and "Prose + Interpreter" ->
        ///   Enhanced keep an upgraded config's original intent.
        /// </summary>
        public static ChatCompositionMode Parse(string dropdownValue)
        {
            if (string.IsNullOrWhiteSpace(dropdownValue)) return ChatCompositionMode.Enhanced;

            string v = dropdownValue.Trim();

            if (string.Equals(v, StandardLabel, StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Standard;
            if (string.Equals(v, EnhancedLabel, StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Enhanced;

            // Legacy migration from the old two-mode dropdown.
            if (string.Equals(v, "Integrated", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Standard;
            if (string.Equals(v, "Prose + Interpreter", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Enhanced;
            if (string.Equals(v, "ProseInterpret", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Enhanced;

            return ChatCompositionMode.Enhanced;
        }
    }
}
