// Code written by Gabriel Mailhot, 26/07/2026.
// The runtime half of the injectable NARRATIVE STYLE feature: which style is active right now, and the lazy
// cache in front of the file. Ratified with Gabriel (2026-07-26): the selection is GLOBAL (one voice for every
// conversation, adult scenes included) and purely RUNTIME (picked in the chat, never an MCM setting), and the
// file is re-read ONLY when the player picks another style or edits the .txt during play. Everything else is
// served from memory, so the per-message cost is a single timestamp check, never a directory scan or a read.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Styles;
using NpcMemoryService.Core.Prompts;

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
        private static bool _activeFileMissing;

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

            // The active file was deleted or renamed mid-play (stamp 0). Do NOT re-scan the folder on every
            // message: the picker and console own directory scanning, never this per-message path. Hold the
            // voice we already loaded (or nothing if we never loaded one). StampTicksOf keeps watching the same
            // name, so if the file returns we pick it up next message without a scan. Logged once per lapse.
            if (stamp == 0L)
            {
                if (!_activeFileMissing)
                {
                    ModLog.Info($"[STYLE] active style '{_selectedName}' file is gone; holding the last-known voice.");
                    _activeFileMissing = true;
                }

                return _cachedRules;
            }

            _activeFileMissing = false;

            if (!StyleCachePolicy.ShouldReload(_cachedName, _selectedName, _cachedStampTicks, stamp)) return _cachedRules;

            _cachedRules = StyleCatalog.ReadRules(_selectedName);
            _cachedName = _selectedName;
            _cachedStampTicks = stamp;
            ModLog.Info($"[STYLE] loaded '{_selectedName}' ({_cachedRules.Length} chars) into the prompt.");

            // A file over the injection ceiling is trimmed at the prompt (PromptBuilder caps it), so warn here,
            // where the file name is known, that the tail of the voice will not reach the model. Logged only on
            // load, so once per edit, not per message.
            if (_cachedRules.Length > PromptBuilder.MaxNarrativeStyleChars)
                ModLog.Info($"[STYLE] WARNING: '{_selectedName}' is {_cachedRules.Length} chars, over the {PromptBuilder.MaxNarrativeStyleChars}-char cap; it will be trimmed in the prompt.");

            return _cachedRules;
        }
    }
}
