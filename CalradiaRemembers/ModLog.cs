// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Settings;
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

      private static readonly HashSet<string> SeenCompat = new();

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
      ///   Master logging switch from the MCM menu. Defaults to disabled when MCM has not
      ///   yet loaded, so players never see logging active without having turned it on.
      ///   When off, no on-screen messages and no file writes — the mod runs silent.
      /// </summary>
      private static bool LoggingEnabled => CrSettings.Instance?.EnableLogging ?? false;

      /// <summary>
      ///   A compatibility notice that is ALWAYS recorded, even when the MCM logging switch is off (its
      ///   default on Nexus). Use ONLY for the "the installed game build does not expose an API this mod
      ///   calls" case: without an always-on trace the mod could quietly degrade after a TaleWorlds change
      ///   and no one would know. It states the FACT only (which method the build does not support) and
      ///   never guesses whether the game or the mod is the one out of date, since it cannot know: an
      ///   OLDER game predates the API, a NEWER game may have changed it. Deduplicated per distinct
      ///   message per session so a per-conversation fault cannot spam the log or the screen. Writes to
      ///   the log file (like the session header) and shows one in-game message so it cannot pass unnoticed.
      /// </summary>
      internal static void Compat(string message)
      {
         lock (SeenCompat)
         {
            if (!SeenCompat.Add(message)) return;
         }

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
      ///   Logs a caught exception, routing the API-mismatch family (a member/type the compiled mod
      ///   expects but the running game build does not provide) to the always-on <see cref="Compat" />
      ///   trace, and everything else to the ordinary (toggle-gated) <see cref="Error" />. This is how
      ///   the mod's existing catch boundaries, SafeRun on every tick and the verb-teaching guard on
      ///   every conversation, cover the whole exercised TaleWorlds surface without wrapping each call:
      ///   any missing-API failure on a guarded path leaves a trace instead of degrading silently.
      /// </summary>
      internal static void Fault(string label, Exception ex)
      {
         if (ex != null && IsApiMismatch(ex))
            Compat($"the installed Bannerlord version does not support a method \"{label}\" calls " +
                   $"({ex.GetType().Name}: {ex.Message}).");
         else
            Error($"{label} failed: {ex?.GetType().Name}: {ex?.Message}");
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

      #region private

      /// <summary>
      ///   True for the exception family that means "the compiled mod references a member or type the
      ///   running game assembly does not have" — the signature of a game/mod version mismatch.
      ///   MissingMemberException is the base of MissingMethodException and MissingFieldException.
      /// </summary>
      private static bool IsApiMismatch(Exception ex)
         => ex is MissingMemberException || ex is TypeLoadException || ex is EntryPointNotFoundException;

      #endregion
   }
}