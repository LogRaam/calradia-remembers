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

      /// <summary>
      ///   Everything a report is made of, for the conversation opened at <paramref name="conversationOpened" />: its exchanges with
      ///   the model and its part of this session's log only (Gabriel, 14/11/2026: "le MODEL EXCHANGE est contextualisé pour la
      ///   conversation en cours?").
      /// </summary>
      internal static PlayerReportFacts Gather(string playerText, bool removeConversationText, IEnumerable<string> transcript, string characterName,
                                               DateTime conversationOpened)
         => new() {
            PlayerText = playerText, RemoveConversationText = removeConversationText, Header = Header(characterName, conversationOpened),
            Transcript = (transcript ?? Enumerable.Empty<string>()).ToList(), Exchanges = Services.ReportRecorder.Instance.Since(conversationOpened),
            LogLines = PlayerReportComposer.LinesSince(SessionLogLines(), conversationOpened.TimeOfDay)
         };

      /// <summary>
      ///   Writes the report whole to a new file in <see cref="Folder" />, and into the log without its log lines, which are
      ///   already there above it (Gabriel, 14/11/2026: "il faudrait pas réinjecter le log au complet"). Returns the file's path,
      ///   or null when the file could not be written (the log still holds the report).
      /// </summary>
      internal static string Write(PlayerReportFacts facts, DateTime conversationOpened)
      {
         ModLog.Report(PlayerReportComposer.Compose(new PlayerReportFacts {
            PlayerText = facts?.PlayerText, RemoveConversationText = facts?.RemoveConversationText == true, Header = facts?.Header,
            Transcript = facts?.Transcript, Exchanges = facts?.Exchanges,
            LogPointer = $"(this conversation's log lines are above in this file, from {conversationOpened:HH:mm:ss}; the report file holds them too)"
         }));
         try
         {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, $"CR_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, PlayerReportComposer.Compose(facts));

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

      private static List<string> Header(string characterName, DateTime conversationOpened)
      {
         var header = new List<string> {
            $"Calradia Remembers: {ModVersion()}",
            $"Bannerlord: {Safe(() => ApplicationVersion.FromParametersFile().ToString())}",
            $"Date: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Character spoken to: {characterName ?? "(none)"}",
            $"Conversation opened: {conversationOpened:HH:mm:ss}",
            $"Model mode: {Safe(() => ModelModePolicy.DisplayName(Services.LlmServices.ActiveModelMode, Foundation.ModSettings.DramaturgeMode))}",
            $"Model: {Safe(() => Services.LlmServices.ActiveModelId)}",
            $"Interpreter model: {Safe(() => NpcMemoryCampaignBehavior.Instance?.LlmServices?.ResolveInterpreterModel())}",
            $"Compact prompt: {(ModSettings.LeanPrompt ? "on" : "off")}",
            // hype (23/11/2026): a report of lost history could not say whether CR had trimmed it to fit a window.
            $"Local model context: {(ModSettings.LocalContextTokens > 0 ? ModSettings.LocalContextTokens + " tokens" : "not declared")}",
            $"Reply token budget: {Safe(() => ModSettings.ReplyTokenBudget?.ToString() ?? "auto")}",
            $"Last context fit: {LocalContextState.LastFit ?? "none (nothing was fitted)"}",
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
