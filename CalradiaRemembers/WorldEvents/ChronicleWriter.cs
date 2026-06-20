// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 (chronicle): renders the player's WorldEvents as a chronicler-voice biography. Phase 1 is
// the free templated tier; an optional LLM-illuminated tier and an in-game viewer come later.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.WorldEvents
{
    /// <summary>
    ///   Turns <see cref="WorldEventStore.GetPlayerChronicle"/> into a medieval-chronicle text and
    ///   writes it to <c>CalradiaRemembers_chronicle.log</c>. Deterministic, zero LLM.
    /// </summary>
    public static class ChronicleWriter
    {
        private static readonly string[] Seasons = { "spring", "summer", "autumn", "winter" };
        private const int DaysPerYear   = 84;
        private const int DaysPerSeason = 21;

        /// <summary>The full chronicle as chronicler-voice prose, dated and in order.</summary>
        public static string Render()
        {
            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds == null || deeds.Count == 0)
                return "The chronicle of your deeds is yet unwritten.";

            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            var sb = new StringBuilder();
            sb.AppendLine($"THE CHRONICLE OF {player.ToUpperInvariant()}");
            sb.AppendLine();

            foreach (WorldEvent e in deeds)
            {
                string line = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.AppendLine($"{DatePhrase(e.Day)}, {line}.");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Writes the rendered chronicle to the Bannerlord Logs folder. Returns the path, or null on failure.</summary>
        public static string WriteToLog()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Mount and Blade II Bannerlord", "Logs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "CalradiaRemembers_chronicle.log");
                File.WriteAllText(path, Render());
                return path;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[CHRONICLE] write failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>"In the {season} of the {Nth} year" for an in-game day (shared with the viewer).</summary>
        public static string DatePhrase(int day)
        {
            int year   = day / DaysPerYear + 1;
            int season = day % DaysPerYear / DaysPerSeason;
            if (season < 0 || season > 3) season = 0;
            return $"In the {Seasons[season]} of the {Ordinal(year)} year";
        }

        private static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return $"{n}th";
            switch (n % 10)
            {
                case 1:  return $"{n}st";
                case 2:  return $"{n}nd";
                case 3:  return $"{n}rd";
                default: return $"{n}th";
            }
        }
    }
}
