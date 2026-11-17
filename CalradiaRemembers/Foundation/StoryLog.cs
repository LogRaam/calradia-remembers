// Code written by Gabriel Mailhot, 06/06/2026.

#region

using System;
using System.IO;

#endregion

namespace CalradiaRemembers.Foundation
{
    /// <summary>
    ///   Writes a clean, narrative-style transcript of conversations and letters to
    ///   <c>CalradiaRemembers_story.log</c>. Reads like a novel — no timestamps,
    ///   no technical metadata. Controlled by the MCM "Story Log" toggle.
    /// </summary>
    internal static class StoryLog
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Logs",
            "CalradiaRemembers_story.log");

        internal static bool IsEnabled => CrSettings.Instance?.StoryLogging ?? false;

        internal static void WriteSessionHeader()
        {
            if (!IsEnabled) return;
            Write($"{Environment.NewLine}=== SESSION {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
        }

        /// <summary>
        ///   Appends one paragraph the story archive rendered (StoryArchiveNovel.Render) when the Story Log is on.
        ///   Since 17/11/2026 every line, deed and letter reaches this file through the story archive (Gabriel: "remplacer
        ///   le Story log par ce nouveau systeme"), which is written always; this switch only decides whether the
        ///   novel-like copy is written too.
        /// </summary>
        internal static void WriteRendered(string paragraph)
        {
            if (!IsEnabled || string.IsNullOrEmpty(paragraph)) return;
            Write(paragraph);
        }

        /// <summary>
        ///   Same player report as ModLog's (2026-07-23): Bannerlord creates
        ///   Documents\Mount and Blade II Bannerlord, but NOT its Logs subfolder, and File.AppendAllText
        ///   throws on a missing parent rather than creating it. With the write wrapped in an empty catch
        ///   (correct in itself: a log must never crash the game) a player who switched Story Log on simply
        ///   got no file, and no reason why. Creating the folder is attempted once per session, on the first
        ///   failure only, so the ordinary path stays a single append with no extra filesystem call.
        /// </summary>
        private static void Write(string text)
        {
            try
            {
                File.AppendAllText(LogPath, text);
                _folderRepairTried = false;
            }
            catch
            {
                if (_folderRepairTried) return; // already tried this session; stay quiet rather than churn

                _folderRepairTried = true;

                try
                {
                    string dir = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(LogPath, text);
                    _folderRepairTried = false;
                }
                catch { /* never crash over a log failure */ }
            }
        }

        private static bool _folderRepairTried;
    }
}
