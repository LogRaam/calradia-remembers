// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 (chronicle): renders the player's WorldEvents as a chronicler-voice biography. Phase 1 is
// the free templated tier; an optional LLM-illuminated tier and an in-game viewer come later.
// 04/07/2026: entries are grouped under yearly headings ("IN THE Nth YEAR") so a long life reads in
// chapters — the year math itself lives in the pure, tested CalradiaRemembers.Logic.ChronicleCalendar
// so this writer and the illuminated viewer (ChronicleViewModel) never disagree on what "year N" means.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CalradiaRemembers.Logic;
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
        /// <summary>Shown when the player has no chronicle-worthy deed recorded yet.</summary>
        public const string EmptyStateLine =
            "No chronicler has yet set quill to your life. In time this page will hold your great battles and sieges, " +
            "the oaths you swore and the crowns you won, the marriages and births of your line, the judgements you passed, " +
            "and the captivities and peaces that shaped your fate.";

        private static readonly string[] Seasons = { "spring", "summer", "autumn", "winter" };
        private const int DaysPerSeason = 21;

        /// <summary>The full chronicle as chronicler-voice prose, dated and in order, grouped by year.</summary>
        public static string Render()
        {
            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds == null || deeds.Count == 0)
                return EmptyStateLine;

            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            var sb = new StringBuilder();
            sb.AppendLine($"THE CHRONICLE OF {player.ToUpperInvariant()}");
            sb.AppendLine();

            var lastYear = -1;
            foreach (WorldEvent e in deeds)
            {
                string line = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
                if (string.IsNullOrWhiteSpace(line)) continue;

                int year = ChronicleCalendar.YearOf(e.Day);
                if (year != lastYear)
                {
                    sb.AppendLine($"{YearHeading(year).ToUpperInvariant()}");
                    sb.AppendLine();
                    lastYear = year;
                }

                sb.AppendLine($"{SeasonPhrase(e.Day)}, {line}.");
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
            => $"{SeasonPhrase(day)} of the {Ordinal(ChronicleCalendar.YearOf(day))} year";

        /// <summary>"In the {season}", without the year — used inside a year heading, which already names the year.</summary>
        private static string SeasonPhrase(int day)
        {
            int season = day % ChronicleCalendar.DaysPerYear / DaysPerSeason;
            if (season < 0 || season > 3) season = 0;
            return $"In the {Seasons[season]}";
        }

        /// <summary>"In the {Nth} year" — the chapter heading grouping entries by chronicle year (shared with the viewer).</summary>
        public static string YearHeading(int year) => $"In the {Ordinal(year)} year";

        /// <summary>The English ordinal suffix for a small positive number ("1st", "2nd", "3rd", "4th"...).</summary>
        public static string Ordinal(int n)
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
