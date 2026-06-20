// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 chronicle: optional LLM "illumination" tier — rewrites the player's deeds into flowing
// chronicler prose. On-demand (costs tokens), cached in memory; the viewer shows it when present.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chronicle
{
    /// <summary>
    ///   Runs (and caches) the optional LLM-illuminated chronicle. The free templated chronicle
    ///   always works; this richer prose is generated on demand and shown by the viewer when ready.
    /// </summary>
    public static class ChronicleIlluminationService
    {
        private static string _illuminated;
        private static bool _busy;

        public static bool IsBusy => _busy;
        public static bool HasIllumination => !string.IsNullOrWhiteSpace(_illuminated);
        public static string Illuminated => _illuminated;

        /// <summary>Kicks off an async illumination pass. Returns a status line for the console.</summary>
        public static string Request()
        {
            if (_busy) return "The chronicle is already being illuminated — give it a moment.";

            ILlmClient client = NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;
            if (client == null) return "No AI is configured — set up a provider and key first.";

            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds == null || deeds.Count == 0) return "There are no deeds to illuminate yet.";

            string deedText = BuildDeedText(deeds);
            string style = LoadStyle();
            string player = Hero.MainHero?.Name?.ToString() ?? "the player";

            _busy = true;
            var illuminator = new ChronicleIlluminator(client);
            _ = Task.Run(async () =>
            {
                try
                {
                    string result = await illuminator.IlluminateAsync(player, deedText, style).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(result)) _illuminated = result;
                    else ModLog.Error("[CHRONICLE] illumination returned empty.");
                }
                catch (Exception ex)
                {
                    ModLog.Error($"[CHRONICLE] illumination failed: {ex.GetType().Name}: {ex.Message}");
                }
                finally { _busy = false; }
            });

            return "Illuminating the chronicle.. reopen it in a few seconds.";
        }

        private static string BuildDeedText(IReadOnlyList<WorldEvent> deeds)
        {
            var sb = new StringBuilder();
            foreach (WorldEvent e in deeds)
            {
                string line = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.AppendLine($"{ChronicleWriter.DatePhrase(e.Day)}: {line}");
            }
            return sb.ToString();
        }

        private static string LoadStyle()
        {
            try
            {
                string path = Path.Combine(
                    BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", "chronicle_style.txt");
                return File.Exists(path) ? File.ReadAllText(path) : "";
            }
            catch { return ""; }
        }
    }
}
