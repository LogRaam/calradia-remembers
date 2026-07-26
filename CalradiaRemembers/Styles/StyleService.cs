// Code written by Gabriel Mailhot, 26/07/2026.
// The runtime half of the injectable NARRATIVE STYLE feature: which style is active right now, and the lazy
// cache in front of the file. Ratified with Gabriel (2026-07-26): the selection is GLOBAL (one voice for every
// conversation, adult scenes included) and purely RUNTIME (picked in the chat, never an MCM setting), and the
// file is re-read ONLY when the player picks another style or edits the .txt during play. Everything else is
// served from memory, so the per-message cost is a single timestamp check, never a directory scan or a read.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Styles;

#endregion

namespace CalradiaRemembers.Styles
{
    /// <summary>Holds the active narrative style and serves its rules to the prompt, re-reading only when it must.</summary>
    public static class StyleService
    {
        private static string _cachedName;
        private static string _cachedRules = string.Empty;
        private static long _cachedStampTicks;
        private static string _selectedName;

        /// <summary>
        ///   Whether the chat window shows its style picker button. ON by default since Gabriel validated the
        ///   picker in play (2026-07-26): it is an ordinary part of the window now, not a test switch.
        ///   <c>cr.style_ui off</c> still hides it for a player who wants the header bare.
        /// </summary>
        public static bool PickerEnabled { get; set; } = true;

        /// <summary>The style currently in force, resolved against the files actually present.</summary>
        public static string ActiveName => string.IsNullOrWhiteSpace(_selectedName)
            ? StyleCatalogPolicy.DefaultStyleName
            : _selectedName;

        /// <summary>
        ///   Picks a style by name (case-insensitive). Scans the folder, so this belongs to the picker and the
        ///   console, never the per-message path. An unknown name degrades to the default rather than failing.
        ///   Returns the name that actually took effect.
        /// </summary>
        public static string Select(string requested)
        {
            IReadOnlyList<string> available = StyleCatalog.AvailableNames();
            _selectedName = StyleCatalogPolicy.ResolveName(requested, available);
            ModLog.Info($"[STYLE] selected '{_selectedName}' (asked for '{requested}').");

            return _selectedName;
        }

        /// <summary>
        ///   The active style's rules, prompt-ready, or "" when there is nothing to inject. Called once per
        ///   message while the encounter is built: it stats ONE known file (microseconds, against an LLM call
        ///   that takes seconds) and re-reads only when <see cref="StyleCachePolicy" /> says the selection or
        ///   the file itself changed, which is what lets a modder fine-tune a style mid-game.
        /// </summary>
        public static string CurrentRules()
        {
            // First call of the session: nothing has been picked yet, so resolve once against what is on disk.
            if (string.IsNullOrWhiteSpace(_selectedName)) Select(null);

            long stamp = StyleCatalog.StampTicksOf(_selectedName);

            // The selected file was deleted or renamed while it was in force: re-resolve once (a rare path), so
            // the conversation degrades to the default voice instead of silently losing its style.
            if (stamp == 0L)
            {
                Select(null);
                stamp = StyleCatalog.StampTicksOf(_selectedName);
            }

            if (!StyleCachePolicy.ShouldReload(_cachedName, _selectedName, _cachedStampTicks, stamp)) return _cachedRules;

            _cachedRules = StyleCatalog.ReadRules(_selectedName);
            _cachedName = _selectedName;
            _cachedStampTicks = stamp;
            ModLog.Info($"[STYLE] loaded '{_selectedName}' ({_cachedRules.Length} chars) into the prompt.");

            return _cachedRules;
        }
    }
}
