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