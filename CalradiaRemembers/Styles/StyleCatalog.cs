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
                               .Where(n => !string.IsNullOrWhiteSpace(n))
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
        ///   Creates the Styles folder on first use and seeds it with a README plus two starter styles, so the
        ///   feature is discoverable and testable without the player authoring anything first. Never overwrites
        ///   a file that already exists, so an edited style survives every launch.
        /// </summary>
        public static void EnsureFolder()
        {
            try
            {
                if (!Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);

                WriteIfAbsent("README.txt", ReadmeText);
                WriteIfAbsent("Default.txt", DefaultStyleText);
                WriteIfAbsent("Novelistic.txt", NovelisticStyleText);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[STYLE] could not create the Styles folder: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #region private

        private static void WriteIfAbsent(string fileName, string content)
        {
            string path = Path.Combine(FolderPath, fileName);
            if (File.Exists(path)) return;

            File.WriteAllText(path, content);
            ModLog.Info($"[STYLE] seeded {fileName} in the Styles folder.");
        }

        private const string ReadmeText =
            "CALRADIA REMEMBERS, NARRATIVE STYLES\r\n" +
            "\r\n" +
            "Every .txt file in this folder is a STYLE: a set of rules describing HOW the AI writes, not who the\r\n" +
            "characters are. The file name is the name you will see in the game's style picker, so \"Novelistic.txt\"\r\n" +
            "shows up as Novelistic.\r\n" +
            "\r\n" +
            "A style is injected into every conversation, adult scenes included, and is shared by both models (the\r\n" +
            "roleplay one and the NSFW one). It never changes a character's personality, what they know, or what\r\n" +
            "they are willing to do: those are decided elsewhere and always win.\r\n" +
            "\r\n" +
            "To write your own: copy one of these files, rename it, and edit the text. Plain English instructions\r\n" +
            "work best, in the second person (\"Write in...\", \"Keep...\").\r\n" +
            "\r\n" +
            "You can edit a style WHILE PLAYING: save the file and your next message uses the new rules, with no\r\n" +
            "restart. Switching style in the picker takes effect on your next message too.\r\n";

        private const string DefaultStyleText =
            "Write as the mod normally does: natural prose, in character, with stage directions in *asterisks*\r\n" +
            "and spoken words in quotes.\r\n" +
            "Keep replies proportionate to the moment: short when the beat is small, fuller when it matters.\r\n";

        private const string NovelisticStyleText =
            "Write this scene as a page from a good novel.\r\n" +
            "Ground every beat in the physical room: what is seen, heard, and touched right now, in specifics\r\n" +
            "rather than abstractions.\r\n" +
            "Vary your sentence rhythm. Let a short sentence land after a long one.\r\n" +
            "Prefer a telling gesture to a stated feeling: show the hand that tightens, not the anger itself.\r\n" +
            "Other characters present are people, not scenery. They have their own interests and may act on them,\r\n" +
            "speak to each other, or press a point of their own, then yield the floor back.\r\n" +
            "Never answer words that were not actually said. If someone only gestured, respond to the gesture as a\r\n" +
            "gesture, and name it plainly so the reader can follow what is passing between you.\r\n" +
            "Avoid stock phrasings and never reuse an image you have already used in this conversation.\r\n";

        #endregion
    }
}
