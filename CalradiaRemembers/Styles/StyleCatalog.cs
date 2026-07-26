// Code written by Gabriel Mailhot, 26/07/2026.
// The disk half of the injectable NARRATIVE STYLE feature: the Styles folder under ModuleData, one .txt per
// style, the file name being the style's display name (the same filename-is-identity convention ChatPortraits
// already uses, so players and modders need no schema). Everything here touches the file system; the decisions
// (which style applies, and whether the cache must be re-read) are pure and live in CalradiaRemembers.Logic.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CalradiaRemembers.Logic.Styles;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Styles
{
    /// <summary>Reads the editable style files shipped in (and added to) <c>ModuleData/Styles</c>.</summary>
    public static class StyleCatalog
    {
        private const string StylesFolderName = "Styles";

        /// <summary>The full path of the Styles folder, whether or not it exists yet.</summary>
        public static string FolderPath
            => Path.Combine(BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", StylesFolderName);

        /// <summary>
        ///   Every style the player currently has, by display name (the file name without its extension),
        ///   alphabetically. Enumerates the directory, so it is called when LISTING or PICKING a style, never
        ///   on the per-message path (which only ever stats one known file).
        /// </summary>
        public static IReadOnlyList<string> AvailableNames()
        {
            try
            {
                EnsureFolder();

                return Directory.Exists(FolderPath)
                    ? Directory.GetFiles(FolderPath, "*.txt")
                               .Select(Path.GetFileNameWithoutExtension)
                               // Documentation (README) and drafts (a leading underscore) are not styles: they
                               // must never show up in the picker as if they were (player report 2026-07-26).
                               .Where(StyleCatalogPolicy.IsSelectableStyleFile)
                               .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                               .ToList()
                    : new List<string>();
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not list the Styles folder: {ex.GetType().Name}: {ex.Message}");

                return new List<string>();
            }
        }

        /// <summary>
        ///   The quill image shown on the chat's style button, shipped as a mod asset (not a player file).
        ///   Optional by design: when it is absent the header falls back to a text button, so a missing or
        ///   unreadable asset can never leave an empty button in the window.
        /// </summary>
        public static string IconPath
            => Path.Combine(BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", "UI", "quill.png");

        /// <summary>True when the quill asset is on disk, so the header can show the icon instead of the label.</summary>
        public static bool IconExists
        {
            get
            {
                try { return File.Exists(IconPath); }
                catch { return false; }
            }
        }

        /// <summary>The file path a style name maps to (never null; the file may not exist).</summary>
        public static string PathOf(string name)
            => Path.Combine(FolderPath, (name ?? string.Empty) + ".txt");

        /// <summary>
        ///   The style file's last-write time in ticks, or 0 when it does not exist. This is the CHEAP check
        ///   (a stat, not a read) the per-message path runs so a modder can fine-tune a style during play:
        ///   one stat before an LLM call that takes seconds costs nothing measurable.
        /// </summary>
        public static long StampTicksOf(string name)
        {
            try
            {
                string path = PathOf(name);

                return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not stat '{name}': {ex.GetType().Name}: {ex.Message}");

                return 0L;
            }
        }

        /// <summary>Reads a style's rules, or "" when the file is missing or unreadable (the prompt then renders nothing).</summary>
        public static string ReadRules(string name)
        {
            try
            {
                string path = PathOf(name);

                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not read '{name}': {ex.GetType().Name}: {ex.Message}");

                return string.Empty;
            }
        }

        /// <summary>
        ///   Makes sure the Styles folder exists so the picker and the console always have a real path to point
        ///   at. The styles THEMSELVES ship with the mod (ModuleData/Styles, deployed like every other editable
        ///   prompt file), so nothing is written here: a player who edits a style keeps their version, and a
        ///   player who deletes one gets it back on the next update rather than having it silently recreated.
        /// </summary>
        public static void EnsureFolder()
        {
            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not create the Styles folder: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
