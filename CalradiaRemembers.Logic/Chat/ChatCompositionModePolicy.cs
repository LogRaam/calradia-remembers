// Code written by Gabriel Mailhot, 16/08/2026.
// Parses the "Chat Composition" MCM dropdown string into the ChatCompositionMode enum. Pure and unit-tested so the
// label strings and the enum can never drift: the MCM dropdown is built from the same label constants this parser
// matches on. ENHANCED is the default (richest experience), so anything blank or unrecognised degrades to Enhanced.
// Migration: the legacy labels "Integrated" and "Prose + Interpreter" map to Standard and Enhanced respectively, so
// a config from the old two-mode era keeps its intent.

#region

using System;
using CalradiaRemembers.Logic.Providers;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
    /// <summary>Maps the "Chat Composition" MCM dropdown value to <see cref="ChatCompositionMode" />.</summary>
    public static class ChatCompositionModePolicy
    {
        /// <summary>The dropdown label for the cheap single integrated call, content-routed (PROSE for chat, NSFW for captivity).</summary>
        public const string StandardLabel = "Standard";

        /// <summary>The dropdown label for the full PROSE + INTERPRET pipeline with the NSFW fallback (the default).</summary>
        public const string EnhancedLabel = "Enhanced";

        /// <summary>
        ///   The dropdown label for the Dramaturge mode (Gabriel, 28/11/2026: "le mode dramaturge avec
        ///   DRAMA+PROSE+INTERPRET+NSFW"). It composes as Enhanced, plus the per-turn machinery only it runs.
        /// </summary>
        public const string DramaturgeLabel = "Dramaturge";

        /// <summary>
        ///   Whether a dropdown value is the Dramaturge mode. The one reader of the third mode: the composition stays
        ///   Enhanced (<see cref="Parse" />), so every test of Enhanced already holds for it, and only the machinery that
        ///   belongs to Dramaturge asks this. Blank, legacy or unknown values are not Dramaturge.
        /// </summary>
        public static bool IsDramaturge(string dropdownValue)
            => !string.IsNullOrWhiteSpace(dropdownValue) && string.Equals(dropdownValue.Trim(), DramaturgeLabel, StringComparison.OrdinalIgnoreCase);

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
            // Dramaturge composes as Enhanced (PROSE + INTERPRET + NSFW); its own machinery asks IsDramaturge.
            if (string.Equals(v, DramaturgeLabel, StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Enhanced;

            // Legacy migration from the old two-mode dropdown.
            if (string.Equals(v, "Integrated", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Standard;
            if (string.Equals(v, "Prose + Interpreter", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Enhanced;
            if (string.Equals(v, "ProseInterpret", StringComparison.OrdinalIgnoreCase)) return ChatCompositionMode.Enhanced;

            return ChatCompositionMode.Enhanced;
        }
    }
}
