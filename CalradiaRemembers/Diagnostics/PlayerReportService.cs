// Code written by Gabriel Mailhot, 14/11/2026.
// The player's "report a mistake": gathers what the mod holds about the conversation, composes the report
// (PlayerReportComposer), writes it to a file the player joins on Discord, and writes it whole into the log. Gabriel
// (14/11/2026): "On clique sur le bouton et on voit tout le contexte, les logs, la conversation et le joueur peut ajouter un
// texte explicatif du problème"; "L'idéal serait de réinjecter le rapport du joueur dans le log avec tout le contexte".
// Version 1 sends nothing over the network: the player sees what is in the file and shares it themself.

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Diagnostics;
using CalradiaRemembers.Logic.Providers;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Diagnostics
{
   /// <summary>Gathers, composes and writes a player report.</summary>
   internal static class PlayerReportService
   {
      /// <summary>Where the player shares the report.</summary>
      internal const string DiscordInvite = "https://discord.gg/Cs6cPTZTGs";

      /// <summary>The most log lines of this session a report carries (the latest).</summary>
      internal const int MaxLogLines = 800;

      private const string SessionMarker = "=== Session started ";

      /// <summary>The folder reports are written to, beside the log.</summary>
      internal static string Folder => Path.Combine(Path.GetDirectoryName(ModLog.FilePath) ?? "", "CR Reports");

      /// <summary>Everything a report is made of, from the conversation open now and what the mod holds.</summary>
      internal static PlayerReportFacts Gather(string playerText, bool removeConversationText, IEnumerable<string> transcript, string characterName)
         => new() {
            PlayerText = playerText, RemoveConversationText = removeConversationText, Header = Header(characterName),
            Transcript = (transcript ?? Enumerable.Empty<string>()).ToList(), Exchanges = Services.ReportRecorder.Instance.Latest(), LogLines = SessionLogLines()
         };

      /// <summary>
      ///   Writes the report to a new file in <see cref="Folder" /> and whole into the log. Returns the file's path, or null when
      ///   the file could not be written (the log still holds it).
      /// </summary>
      internal static string Write(string report)
      {
         ModLog.Report(report);
         try
         {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"CR_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, report ?? "");

            return path;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[REPORT] the report file could not be written: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>Shows the file in the Explorer and puts the Discord invite in the clipboard. Never throws.</summary>
      internal static void ShowAndShare(string path)
      {
         try { if (path != null) Process.Start("explorer.exe", $"/select,\"{path}\""); }
         catch { /* the path is also shown in the message */ }
         try { TaleWorlds.InputSystem.Input.SetClipboardText(DiscordInvite); }
         catch { /* the invite is also shown in the message */ }
      }

      /// <summary>The lines of this session's log, the latest <see cref="MaxLogLines" />; empty when there is no log.</summary>
      internal static IReadOnlyList<string> SessionLogLines()
      {
         try
         {
            if (!File.Exists(ModLog.FilePath)) return new List<string>();

            string[] lines;
            using (var stream = new FileStream(ModLog.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
               lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');

            int start = Array.FindLastIndex(lines, l => l.StartsWith(SessionMarker, StringComparison.Ordinal));
            IEnumerable<string> session = lines.Skip(Math.Max(0, start));

            return session.Reverse().Take(MaxLogLines).Reverse().ToList();
         }
         catch (Exception ex)
         {
            return new List<string> {$"(the log could not be read: {ex.GetType().Name})"};
         }
      }

      private static List<string> Header(string characterName)
      {
         var header = new List<string> {
            $"Calradia Remembers: {ModVersion()}",
            $"Bannerlord: {Safe(() => ApplicationVersion.FromParametersFile().ToString())}",
            $"Date: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Character spoken to: {characterName ?? "(none)"}",
            $"Model mode: {Safe(() => ModelModePolicy.DisplayName(Services.LlmServices.ActiveModelMode))}",
            $"Model: {Safe(() => Services.LlmServices.ActiveModelId)}",
            $"Interpreter model: {Safe(() => NpcMemoryCampaignBehavior.Instance?.LlmServices?.ResolveInterpreterModel())}",
            $"Compact prompt: {(ModSettings.LeanPrompt ? "on" : "off")}",
            $"Adult content: {ModSettings.AdultContentLevel}",
            $"Reply language: {Safe(() => ModSettings.ReplyLanguage)}",
            $"File logging: {(ModLog.AnyChannelEnabled ? "on" : "off")}"
         };

         return header;
      }

      private static string ModVersion()
      {
         try
         {
            string xml = File.ReadAllText(Path.Combine(Paths.ModulePaths.Root, "SubModule.xml"));
            Match m = Regex.Match(xml, "<Version value=\"([^\"]+)\"");

            return m.Success ? m.Groups[1].Value : "(unknown)";
         }
         catch { return "(unknown)"; }
      }

      private static string Safe(Func<string> read)
      {
         try { return string.IsNullOrWhiteSpace(read()) ? "(none)" : read(); }
         catch { return "(unknown)"; }
      }
   }
}
