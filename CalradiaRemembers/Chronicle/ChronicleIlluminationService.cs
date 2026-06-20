// Code written by Gabriel Mailhot, 19/06/2026.
// v1.2 chronicle: drives the optional LLM "illumination" tier. The chronicle is a LIVING manuscript:
// Extend illuminates only the deeds added since the last pass (cheap, append, with continuity);
// RewriteAll redoes the whole thing (e.g. after editing chronicle_style.txt). State persists via
// ChronicleBehavior; the open viewer is refreshed live when an async pass completes.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.LlmClient;
using NpcMemoryService.Core.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chronicle
{
    /// <summary>Generates and persists the illuminated chronicle, incrementally.</summary>
    public static class ChronicleIlluminationService
    {
        private static volatile bool _busy;

        public static bool IsBusy => _busy;
        public static bool HasIllumination => !string.IsNullOrWhiteSpace(ChronicleBehavior.Instance?.IlluminatedText);
        public static string Illuminated => ChronicleBehavior.Instance?.IlluminatedText ?? "";
        public static int ThroughCount => ChronicleBehavior.Instance?.IlluminatedThroughCount ?? 0;

        /// <summary>How many of the player's deeds have not yet been set into illuminated prose.</summary>
        public static int PendingDeedCount()
        {
            int total = WorldEventStore.Instance?.GetPlayerChronicle()?.Count ?? 0;
            return Math.Max(0, total - ThroughCount);
        }

        /// <summary>Illuminates only the deeds added since the last pass, appended to the manuscript.</summary>
        public static string Extend() => Run(rewriteAll: false);

        /// <summary>Redoes the whole chronicle from the first deed (use after changing the style).</summary>
        public static string RewriteAll() => Run(rewriteAll: true);

        private static string Run(bool rewriteAll)
        {
            if (_busy) return "The chronicle is already being illuminated — give it a moment.";
            if (ChronicleBehavior.Instance == null) return "Chronicle state is not ready.";

            ILlmClient client = NpcMemoryCampaignBehavior.Instance?.LlmServices?.LlmClient;
            if (client == null) return "No AI is configured — set up a provider and key first.";

            IReadOnlyList<WorldEvent> deeds = WorldEventStore.Instance?.GetPlayerChronicle();
            if (deeds == null || deeds.Count == 0) return "There are no deeds to illuminate yet.";

            int from = rewriteAll ? 0 : Math.Min(ChronicleBehavior.Instance.IlluminatedThroughCount, deeds.Count);
            if (!rewriteAll && from >= deeds.Count) return "The chronicle is already up to date.";

            string deedText = BuildDeedText(deeds, from);
            string style = LoadStyle();
            string player = Hero.MainHero?.Name?.ToString() ?? "the player";
            string priorTail = rewriteAll ? null : Tail(ChronicleBehavior.Instance.IlluminatedText, 600);
            int total = deeds.Count;

            _busy = true;
            var illuminator = new ChronicleIlluminator(client);
            _ = Task.Run(async () =>
            {
                string result = null;
                try { result = await illuminator.IlluminateAsync(player, deedText, style, priorTail).ConfigureAwait(false); }
                catch (Exception ex) { ModLog.Error($"[CHRONICLE] illumination failed: {ex.GetType().Name}: {ex.Message}"); }

                // State update is a plain string/int swap — safe off the main thread, and it lands even
                // if the viewer was closed before the call returned (persisted, shown on next open).
                if (!string.IsNullOrWhiteSpace(result) && ChronicleBehavior.Instance != null)
                {
                    ChronicleBehavior b = ChronicleBehavior.Instance;
                    b.IlluminatedText = rewriteAll || string.IsNullOrWhiteSpace(b.IlluminatedText)
                        ? result.Trim()
                        : b.IlluminatedText.TrimEnd() + "\n\n" + result.Trim();
                    b.IlluminatedThroughCount = total;
                }
                else ModLog.Error("[CHRONICLE] illumination returned empty.");

                _busy = false;
                MainThreadDispatcher.Enqueue(ChronicleManager.RefreshActive); // best-effort live refresh
            });

            return rewriteAll ? "Rewriting the whole chronicle.." : "The chronicler takes up the latest deeds..";
        }

        private static string BuildDeedText(IReadOnlyList<WorldEvent> deeds, int from)
        {
            var sb = new StringBuilder();
            for (int i = from; i < deeds.Count; i++)
            {
                WorldEvent e = deeds[i];
                string line = string.IsNullOrWhiteSpace(e.ChronicleLine) ? e.Summary : e.ChronicleLine;
                if (string.IsNullOrWhiteSpace(line)) continue;
                sb.AppendLine($"{ChronicleWriter.DatePhrase(e.Day)}: {line}");
            }
            return sb.ToString();
        }

        private static string Tail(string text, int chars)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return text.Length <= chars ? text : text.Substring(text.Length - chars);
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
