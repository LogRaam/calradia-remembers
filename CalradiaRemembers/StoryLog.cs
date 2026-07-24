// Code written by Gabriel Mailhot, 06/06/2026.

#region

using System;
using System.IO;
using CalradiaRemembers.Settings;

#endregion

namespace CalradiaRemembers
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
        ///   Writes the opening line of a conversation: who is present and where.
        ///   Example: "You meet Alympia and Martira in the castle of Poros."
        /// </summary>
        internal static void WriteConversationIntro(
            string   npcName,
            string[] witnessNames,
            string   location,
            int      day)
        {
            if (!IsEnabled) return;

            string others = witnessNames == null || witnessNames.Length == 0
                ? ""
                : witnessNames.Length == 1
                    ? $" and {witnessNames[0]}"
                    : ", " + string.Join(", ", witnessNames, 0, witnessNames.Length - 1)
                      + " and " + witnessNames[witnessNames.Length - 1];

            string where = string.IsNullOrEmpty(location) ? "" : $" in {location}";

            Write($"{Environment.NewLine}[Day {day}]{Environment.NewLine}");
            Write($"{Environment.NewLine}You meet {npcName}{others}{where}.{Environment.NewLine}");
        }

        internal static void WritePlayerLine(string text)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(text)) return;
            Write($"{Environment.NewLine}You: {text}{Environment.NewLine}");
        }

        internal static void WriteNpcLine(string npcName, string text)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(text)) return;
            Write($"{Environment.NewLine}{npcName}: {text}{Environment.NewLine}");
        }

        internal static void WriteWitnessLine(string witnessName, string text)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(text)) return;
            Write($"{Environment.NewLine}{witnessName}: {text}{Environment.NewLine}");
        }

        /// <summary>Scene narration — written as a standalone paragraph, no speaker prefix.</summary>
        internal static void WriteNarration(string text)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(text)) return;
            Write($"{Environment.NewLine}{text}{Environment.NewLine}");
        }

        internal static void WriteRelationChange(string npcName, int delta)
        {
            if (!IsEnabled || delta == 0) return;
            string desc = delta >=  3 ? $"{npcName} is visibly moved by your words."
                        : delta >=  1 ? $"{npcName} seems to appreciate your words."
                        : delta >= -2 ? $"{npcName}'s expression cools slightly."
                        : delta >= -5 ? $"{npcName} takes offense at your words."
                        : delta >= -8 ? $"{npcName}'s regard for you drops sharply."
                        :               $"A cold distance settles over {npcName}.";
            Write($"{Environment.NewLine}{desc}{Environment.NewLine}");
        }

        internal static void WriteGoldTransfer(string npcName, int amount, bool npcGives)
        {
            if (!IsEnabled || amount <= 0) return;
            string desc = npcGives
                ? $"{npcName} hands you {amount} denars."
                : $"You hand {npcName} {amount} denars.";
            Write($"{Environment.NewLine}{desc}{Environment.NewLine}");
        }

        internal static void WriteLetterReceived(string senderName, string content, int day)
        {
            if (!IsEnabled) return;
            Write($"{Environment.NewLine}--- Letter from {senderName} (Day {day}) ---"
                + $"{Environment.NewLine}{Environment.NewLine}{content}"
                + $"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}");
        }

        internal static void WriteLetterSent(string recipientName, string content, int day)
        {
            if (!IsEnabled) return;
            Write($"{Environment.NewLine}--- You send a letter to {recipientName} (Day {day}) ---"
                + $"{Environment.NewLine}{Environment.NewLine}{content}"
                + $"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}");
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
