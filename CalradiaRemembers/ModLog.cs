// Code written by Gabriel Mailhot, 17/05/2026.

#region

using System;
using System.IO;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    /// <summary>
    ///   Writes timestamped log entries to a file in the Bannerlord Logs folder
    ///   AND shows them in the in-game console.
    ///   Log file: %USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\CalradiaRemembers.log
    /// </summary>
    internal static class ModLog
    {
        private const string Tag = "NPC Memory Service";

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Logs",
            "CalradiaRemembers.log");

        /// <summary>
        ///   Master logging switch from the MCM menu. Defaults to disabled when MCM has not
        ///   yet loaded, so players never see logging active without having turned it on.
        ///   When off, no on-screen messages and no file writes — the mod runs silent.
        /// </summary>
        private static bool LoggingEnabled => Settings.CrSettings.Instance?.EnableLogging ?? false;

        static ModLog()
        {
            // Write a header line on each game launch so the log is easy to read.
            try
            {
                File.AppendAllText(LogPath,
                    $"{Environment.NewLine}=== Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
            }
            catch
            {
                /* never crash the game over a log failure */
            }
        }

        /// <summary>
        ///   Logs an error both to the in-game console (in red) and to the log file.
        /// </summary>
        internal static void Error(string message)
        {
            if (!LoggingEnabled) return;
            var line = $"[{DateTime.Now:HH:mm:ss}] [ERROR] [{Tag}] {message}";

            InformationManager.DisplayMessage(
                new InformationMessage($"[{Tag}] ERROR: {message}", Colors.Red));

            try { File.AppendAllText(LogPath, line + Environment.NewLine); }
            catch { }
        }

        /// <summary>
        ///   A compatibility/degradation warning that is ALWAYS recorded, even when the MCM logging switch
        ///   is off (its default on Nexus). Use ONLY for the rare "a game API we relied on is missing on
        ///   this build" case: without an always-on trace the mod could quietly degrade after a TaleWorlds
        ///   update and no one would know. Writes to the log file unconditionally (like the session header)
        ///   and shows one in-game message so it cannot pass unnoticed.
        /// </summary>
        internal static void Compat(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] [COMPAT] [{Tag}] {message}";

            try { File.AppendAllText(LogPath, line + Environment.NewLine); }
            catch { }

            try
            {
                InformationManager.DisplayMessage(
                    new InformationMessage($"[{Tag}] compatibility notice: {message}", Colors.Yellow));
            }
            catch
            {
                /* a UI call before the manager is ready must never crash startup */
            }
        }

        /// <summary>
        ///   Logs a message both to the in-game console and to the log file.
        /// </summary>
        internal static void Info(string message)
        {
            if (!LoggingEnabled) return;
            var line = $"[{DateTime.Now:HH:mm:ss}] [{Tag}] {message}";

            // In-game console (top-left, ephemeral)
            InformationManager.DisplayMessage(new InformationMessage($"[{Tag}] {message}"));

            // File log (persistent, check after the session)
            try { File.AppendAllText(LogPath, line + Environment.NewLine); }
            catch { }
        }
    }
}