// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Logic.Diagnostics;
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
      private static readonly HashSet<string> SeenConfigError = new();
      private static readonly HashSet<string> SeenNotice = new();

      // Player report (2026-07-23): with logging switched ON, nothing was ever written. Bannerlord creates
      // Documents\Mount and Blade II Bannerlord for saves and config, but NOT the Logs subfolder, and
      // File.AppendAllText throws DirectoryNotFoundException rather than creating a missing parent. Every
      // write here is wrapped in an empty catch (correctly: a log must never crash the game), so the failure
      // was perfectly silent and cost the mod its whole diagnostic channel with players.
      private static bool _fileChannelBroken;
      private static bool _brokenNoticeShown;

      // The delimiter every session header starts with; the rotation below splits the file on it.
      private const string SessionMarker = "=== Session started ";

      // How many sessions the log file retains, INCLUDING the one starting now, so it never grows without bound
      // (player request 2026-08-27). Two: the current run and the one before it, which is what a bug report needs.
      private const int SessionsToKeep = 2;

      static ModLog()
      {
         // Write a header line on each game launch so the log is easy to read. Creating the folder is done
         // here, once, rather than at each write: CreateDirectory is a no-op when it already exists, and
         // doing it even while logging is OFF means the folder is ready the moment a player turns it on.
         try
         {
            string dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            TrimToRecentSessions(SessionsToKeep); // drop old sessions BEFORE this run's header goes in

            File.AppendAllText(LogPath,
               $"{Environment.NewLine}=== Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
         }
         catch
         {
            // Still never crash the game over a log failure, but remember that the file channel is dead so a
            // player who deliberately enabled logging is told once, instead of hunting for a file that will
            // never appear.
            _fileChannelBroken = true;
         }
      }

      /// <summary>
      ///   The FILE logging switch from the MCM menu: writes diagnostics to CalradiaRemembers.log. Defaults to
      ///   disabled when MCM has not yet loaded, so a player never gets a log file without having asked for one.
      /// </summary>
      private static bool FileLoggingEnabled => CrSettings.Instance?.EnableLogging ?? false;

      /// <summary>
      ///   The ON-SCREEN logging switch from the MCM menu: prints diagnostics as top-left messages. Separate from
      ///   the file channel (player request 2026-08-27: the on-screen stream crowds the screen during play), and
      ///   off when MCM has not loaded.
      /// </summary>
      private static bool ScreenLoggingEnabled => CrSettings.Instance?.EnableScreenLogging ?? false;

      /// <summary>
      ///   True when either channel would actually record a message. Logging is OFF by default for Nexus, so a
      ///   caller that does real work (reflection, string building) once per game tick or per dialogue state
      ///   change should check this FIRST and skip that work entirely rather than build a message only for
      ///   <see cref="Info" />/<see cref="Error" /> to throw it away.
      /// </summary>
      internal static bool AnyChannelEnabled => FileLoggingEnabled || ScreenLoggingEnabled;

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

         WriteToFile(line);

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
      ///   A player-authored config file (e.g. character_overrides.json) could not be read. ALWAYS recorded,
      ///   even when the MCM logging switch is off (its default on Nexus): the mod's own diagnostics
      ///   (Info/Error) are gated by that switch, so without this channel a player who typos their JSON gets
      ///   zero signal that their edit silently did nothing. Deduplicated per distinct message per session,
      ///   same pattern as <see cref="Compat" />, so a fault on a hot path cannot spam the log or the screen.
      /// </summary>
      internal static void ConfigError(string message)
      {
         lock (SeenConfigError)
         {
            if (!SeenConfigError.Add(message)) return;
         }

         var line = $"[{DateTime.Now:HH:mm:ss}] [CONFIG] [{Tag}] {message}";

         WriteToFile(line);

         try
         {
            InformationManager.DisplayMessage(
               new InformationMessage($"[{Tag}] config error: {message}", Colors.Red));
         }
         catch
         {
            /* a UI call before the manager is ready must never crash startup */
         }
      }

      /// <summary>
      ///   Something the player ASKED FOR did not happen, and they are the only person who can see that it
      ///   did not. ALWAYS recorded, even when the MCM logging switch is off (its default on Nexus), for the
      ///   same reason <see cref="ConfigError" /> is: the ordinary diagnostics are gated by that switch, so a
      ///   player who answers a popup and gets nothing has no way to tell a broken feature from a feature
      ///   that never fired, and neither has anyone reading their report.
      ///   <para>
      ///     Two players said the same sentence a week apart (Lordfadooboo 11/09/2026, Zinced121212
      ///     17/09/2026): "a companion wants to talk, I say yes, and nothing happens". Every branch that
      ///     could swallow that answer wrote its account through <see cref="Info" />, so the log they would
      ///     have sent was empty of it. A channel is the difference between a fix and another guess.
      ///   </para>
      ///   Reserve this for a DEAD END the player experienced. A path that recovers (a fallback that still
      ///   opens the window) belongs on <see cref="Info" />: a notice that fires when nothing went wrong
      ///   teaches players to ignore it. Deduplicated per distinct message per session, same pattern as
      ///   <see cref="Compat" />, so a fault on a repeating path cannot spam the log or the screen.
      /// </summary>
      internal static void Notice(string message)
      {
         lock (SeenNotice)
         {
            if (!SeenNotice.Add(message)) return;
         }

         var line = $"[{DateTime.Now:HH:mm:ss}] [NOTICE] [{Tag}] {message}";

         WriteToFile(line);

         try
         {
            InformationManager.DisplayMessage(
               new InformationMessage($"[{Tag}] {message}", Colors.Yellow));
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
         if (!FileLoggingEnabled && !ScreenLoggingEnabled) return;
         var line = $"[{DateTime.Now:HH:mm:ss}] [ERROR] [{Tag}] {message}";

         if (ScreenLoggingEnabled)
            InformationManager.DisplayMessage(
               new InformationMessage($"[{Tag}] ERROR: {message}", Colors.Red));

         if (FileLoggingEnabled)
            WriteToFile(line);
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
         if (!FileLoggingEnabled && !ScreenLoggingEnabled) return;
         var line = $"[{DateTime.Now:HH:mm:ss}] [{Tag}] {message}";

         // In-game console (top-left, ephemeral): only when on-screen logging is on.
         if (ScreenLoggingEnabled)
            InformationManager.DisplayMessage(new InformationMessage($"[{Tag}] {message}"));

         // File log (persistent, check after the session): only when file logging is on.
         if (FileLoggingEnabled)
            WriteToFile(line);
      }

      #region private

      /// <summary>
      ///   True for the exception family that means "the compiled mod references a member or type the
      ///   running game assembly does not have" — the signature of a game/mod version mismatch.
      ///   MissingMemberException is the base of MissingMethodException and MissingFieldException.
      /// </summary>
      private static bool IsApiMismatch(Exception ex)
         => ex is MissingMemberException || ex is TypeLoadException || ex is EntryPointNotFoundException;

      /// <summary>
      ///   Keeps the log file from growing without bound (player request 2026-08-27): retains only the most
      ///   recent <paramref name="keep" /> sessions INCLUDING the one about to be appended, so it drops all but
      ///   the last <c>keep - 1</c> existing session blocks (split on <see cref="SessionMarker" />). Run once at
      ///   launch, before this run's header. Never throws: log housekeeping must never cost the game a frame, and
      ///   a file that cannot be trimmed is simply left as it is.
      /// </summary>
      private static void TrimToRecentSessions(int keep)
      {
         try
         {
            if (keep < 1 || !File.Exists(LogPath)) return;

            string content = File.ReadAllText(LogPath);
            // Retain keep-1 existing sessions; this run's header is appended right after, for `keep` in total.
            string trimmed = LogRotationPolicy.KeepLastSessions(content, SessionMarker, keep - 1);

            if (!ReferenceEquals(trimmed, content)) File.WriteAllText(LogPath, trimmed);
         }
         catch
         {
            /* housekeeping only; never crash or block the game over an unreadable/locked log file */
         }
      }

      /// <summary>
      ///   The single seam every file write goes through, so the missing-folder repair and the
      ///   broken-channel notice cannot be forgotten at one call site out of five (which is exactly how the
      ///   original bug stayed invisible). Retries the folder once per session on the first failure, since
      ///   a player can delete it mid-session, then gives up quietly for the rest of the run.
      /// </summary>
      private static void WriteToFile(string line)
      {
         try
         {
            File.AppendAllText(LogPath, line + Environment.NewLine);
            _fileChannelBroken = false;
         }
         catch
         {
            if (!_fileChannelBroken)
            {
               _fileChannelBroken = true;

               try
               {
                  string dir = Path.GetDirectoryName(LogPath);
                  if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                  File.AppendAllText(LogPath, line + Environment.NewLine);
                  _fileChannelBroken = false;
               }
               catch
               {
                  /* the retry failed too; the notice below is all that is left */
               }
            }

            NoticeFileChannelBroken();
         }
      }

      /// <summary>
      ///   Tells the player ONCE, on screen, that the log file cannot be written and where it was expected.
      ///   Only ever shown while logging is switched on: a player who deliberately enabled it and then finds
      ///   no file has no other way to learn why, and silence there is what turned a missing folder into an
      ///   unreportable bug. Never shown when logging is off (its Nexus default), where an absent file is
      ///   simply correct.
      /// </summary>
      private static void NoticeFileChannelBroken()
      {
         if (_brokenNoticeShown || !_fileChannelBroken || !FileLoggingEnabled) return;

         _brokenNoticeShown = true;

         try
         {
            InformationManager.DisplayMessage(new InformationMessage(
               $"[{Tag}] logging is ON but the log FILE could not be written: {LogPath}. " +
               "On-screen messages still work. Check that the folder exists and is writable.", Colors.Yellow));
         }
         catch
         {
            /* a UI call before the manager is ready must never crash startup */
         }
      }

      #endregion
   }
}