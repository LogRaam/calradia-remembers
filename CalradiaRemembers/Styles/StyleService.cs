// Code written by Gabriel Mailhot, 26/07/2026.
// The runtime half of the injectable NARRATIVE STYLE feature: which style is active right now, and the lazy
// cache in front of the file. Ratified with Gabriel (2026-07-26): the selection is GLOBAL (one voice for every
// conversation, adult scenes included) and purely RUNTIME (picked in the chat, never an MCM setting), and the
// file is re-read ONLY when the player picks another style or edits the .txt during play. Everything else is
// served from memory, so the per-message cost is a single timestamp check, never a directory scan or a read.
//
// v1.34.3: the SELECTION is now persisted (player report: the style reset to "Default" on every restart and
// every mod update). The chosen NAME is written to a tiny file under the player's Bannerlord user data
// (Configs, resolved via the same GetFolderPath route ModLog uses so a OneDrive-redirected Documents still
// works) and reloaded once at first access, so it survives a restart AND an update (the file lives OUTSIDE the
// module folder, which an update wipes). Only the SELECTION persists; the style CONTENT caching is unchanged.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Foundation;
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
        private static bool _selectionLoaded;

        /// <summary>
        ///   Where the chat-picked style NAME is persisted: a tiny text file under the player's Bannerlord
        ///   user data, NOT inside the module folder (a module-folder file is wiped on every update, which is
        ///   half the bug this fixes). Resolved through <see cref="Environment.GetFolderPath" /> exactly like
        ///   <see cref="ModLog" />, because a player's Documents can be OneDrive-redirected, so a literal
        ///   C:\Users\...\Documents path would be wrong.
        /// </summary>
        private static readonly string SelectionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs",
            "CalradiaRemembers_style.txt");

        /// <summary>
        ///   Whether the chat window shows its style picker button. ON by default since Gabriel validated the
        ///   picker in play (2026-07-26): it is an ordinary part of the window now, not a test switch.
        ///   <c>cr.style_ui off</c> still hides it for a player who wants the header bare.
        /// </summary>
        public static bool PickerEnabled { get; set; } = true;

        /// <summary>The style currently in force, resolved against the files actually present.</summary>
        public static string ActiveName
        {
            get
            {
                EnsureSelectionLoaded();

                return string.IsNullOrWhiteSpace(_selectedName)
                    ? StyleCatalogPolicy.DefaultStyleName
                    : _selectedName;
            }
        }

        /// <summary>
        ///   Picks a style by name (case-insensitive). Scans the folder, so this belongs to the picker and the
        ///   console, never the per-message path. An unknown name degrades to the default rather than failing.
        ///   Returns the name that actually took effect. The choice is persisted so it survives a restart and a
        ///   mod update (v1.34.3).
        /// </summary>
        public static string Select(string requested)
        {
            // Mark the persisted selection as consumed BEFORE overwriting it, so a later first read cannot
            // reload the old file over the fresh pick (the load runs at most once per session either way).
            _selectionLoaded = true;

            IReadOnlyList<string> available = StyleCatalog.AvailableNames();
            _selectedName = StyleCatalogPolicy.ResolveName(requested, available);
            ModLog.Info($"[STYLE] selected '{_selectedName}' (asked for '{requested}').");

            PersistSelection(_selectedName);

            return _selectedName;
        }

        /// <summary>
        ///   Restores the persisted style selection ONCE, before the first read a conversation makes. A
        ///   missing file (first run), a blank one, or a name that no longer matches a style on disk all
        ///   degrade to the default via the pure <see cref="StylePersistencePolicy" />; it never throws, so a
        ///   hand-edited or corrupt file can only cost the persisted choice, never a conversation.
        /// </summary>
        private static void EnsureSelectionLoaded()
        {
            if (_selectionLoaded) return;
            _selectionLoaded = true;

            try
            {
                string raw = File.Exists(SelectionFilePath) ? File.ReadAllText(SelectionFilePath) : null;
                _selectedName = StylePersistencePolicy.ResolvePersistedName(raw, StyleCatalog.AvailableNames());
                ModLog.Info($"[STYLE] restored persisted selection '{_selectedName}'.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not restore the persisted selection: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        ///   Writes the chosen style NAME to the persistence file, creating the Configs folder if needed.
        ///   Never throws: a failed write only costs the persisted choice for next launch, never the pick the
        ///   player just made this session.
        /// </summary>
        private static void PersistSelection(string name)
        {
            try
            {
                string dir = Path.GetDirectoryName(SelectionFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(SelectionFilePath, StylePersistencePolicy.Serialize(name));
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not persist the selection '{name}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        ///   The active style's rules, prompt-ready, or "" when there is nothing to inject. Called once per
        ///   message while the encounter is built: it stats ONE known file (microseconds, against an LLM call
        ///   that takes seconds) and re-reads only when <see cref="StyleCachePolicy" /> says the selection or
        ///   the file itself changed, which is what lets a modder fine-tune a style mid-game.
        /// </summary>
        public static string CurrentRules()
        {
            // First call of the session: restore the persisted pick (v1.34.3) before anything reads the style.
            EnsureSelectionLoaded();

            // Still nothing (no persisted file and nothing picked yet): resolve once against what is on disk.
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
