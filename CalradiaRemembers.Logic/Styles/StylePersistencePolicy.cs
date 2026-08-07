// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.3: the narrative style selection is GLOBAL and RUNTIME (picked in the chat, one voice for every
// conversation, never an MCM setting), so the mod's StyleService held it only in a static field with no
// persistence. Player report: the style reset to "Default" on every restart AND every mod update. The fix
// writes the chosen name to a tiny file in the player's Bannerlord user data (outside the module, so an
// update cannot wipe it) and reloads it at startup. This policy owns the PURE half: turning the raw bytes
// on disk (which may be blank, multi-line, or hand-edited into nonsense) into the canonical style name to
// restore, degrading to the default when the persisted name no longer names a style that exists. Get it
// wrong and a stale or corrupt file either blanks the voice or resurrects a deleted style.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Styles
{
    /// <summary>
    ///   Pure rules for persisting and restoring the chat-picked narrative style SELECTION (a single
    ///   global name). The file's location and IO stay mod-side (they need the engine's Documents-path
    ///   resolution); everything here is testable name arithmetic.
    /// </summary>
    public static class StylePersistencePolicy
    {
        /// <summary>
        ///   The exact text to WRITE for a chosen style name: the resolved name on a single line. Blank or
        ///   null collapses to the default, so the file always names a real style rather than an empty line.
        /// </summary>
        /// <param name="resolvedName">The canonical name that actually took effect.</param>
        /// <returns>The file contents to persist.</returns>
        public static string Serialize(string resolvedName)
        {
            string name = Sanitize(resolvedName);

            return name ?? StyleCatalogPolicy.DefaultStyleName;
        }

        /// <summary>
        ///   The canonical style name to restore from a persisted file's raw contents, validated against
        ///   the styles actually on disk. A missing file (null), a blank one, or a name that no longer
        ///   matches any style all degrade to the default via <see cref="StyleCatalogPolicy.ResolveName" />,
        ///   so a first run or a hand-edited file can never blank the voice or name a style that is gone.
        /// </summary>
        /// <param name="rawFileContent">The persistence file's contents, or null when it does not exist.</param>
        /// <param name="available">The style display names currently on disk.</param>
        /// <returns>The canonical name to make active.</returns>
        public static string ResolvePersistedName(string rawFileContent, IReadOnlyCollection<string> available)
        {
            string requested = Sanitize(rawFileContent);

            return StyleCatalogPolicy.ResolveName(requested, available);
        }

        /// <summary>
        ///   Reduces raw file text to a candidate name: the first non-blank line, trimmed, or null when
        ///   there is nothing usable. A style name is a single line, so trailing lines a player or a text
        ///   editor might leave (a stray newline, a comment on a second line) are ignored rather than
        ///   folded into the name.
        /// </summary>
        /// <param name="text">Raw text, or null.</param>
        /// <returns>The trimmed first non-blank line, or null.</returns>
        public static string Sanitize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed)) return trimmed;
            }

            return null;
        }
    }
}
