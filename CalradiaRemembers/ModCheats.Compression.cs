// Code written by Gabriel Mailhot, 26/07/2026.
// Console surface for memory compression: see what a character's memory is costing, and force a pass without
// waiting for the daily tick. Built for the question that prompted the feature (a modder measuring CR's context
// past 20k tokens), so the size can be measured before and after rather than guessed at.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Memory;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        private const int MemoryReportRows = 10;

        /// <summary>
        ///   cr.memory_load: lists the characters carrying the most memories, since those are the ones inflating
        ///   the prompt, and says whether each is due for compression. Mutates nothing.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("memory_load", "cr")]
        public static string MemoryLoad(List<string> args)
        {
            try
            {
                if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

                List<(string Id, string Name, int Count)> rows = behavior.Store.ListIds()
                    .Select(id =>
                    {
                        NpcProfile p = behavior.Store.Get(id);

                        return (Id: id, Name: p?.Name ?? id, Count: p?.Events?.Count ?? 0);
                    })
                    .Where(r => r.Count > 0)
                    .OrderByDescending(r => r.Count)
                    .Take(MemoryReportRows)
                    .ToList();

                if (rows.Count == 0) return "No character has recorded a memory yet.";

                var sb = new StringBuilder();
                sb.AppendLine($"Longest memories (compression {(ModSettings.MemoryCompression ? "ON" : "OFF")}, "
                            + $"threshold {ModSettings.MemoryCompressionThreshold} events):");

                foreach ((string _, string name, int count) in rows)
                    sb.AppendLine(count >= ModSettings.MemoryCompressionThreshold
                        ? $"  {name}: {count} events  <- due"
                        : $"  {name}: {count} events");

                sb.Append("cr.prompt_size <name> shows what that costs in the prompt; cr.compress forces a pass.");

                return sb.ToString();
            }
            catch (Exception ex) { return $"cr.memory_load failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>
        ///   cr.save_sizes: what each CR store weighed at the last save of this session, raw and in the save, against the
        ///   engine's ceiling (tashmetu, 09/11/2026: a store past 16,777,215 bytes is saved without error and never loads).
        ///   Mutates nothing.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("save_sizes", "cr")]
        public static string SaveSizes(List<string> args)
        {
            try
            {
                var rows = Foundation.SaveBlob.LastSaved;
                if (rows.Count == 0) return "No CR store has been saved in this session yet: save the game, then run cr.save_sizes again.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"CR stores at the last save (the game cannot load one past {Logic.Saving.SaveBlobCodec.EngineCeiling:N0} bytes; "
                              + $"CR refuses to write one at {Logic.Saving.SaveBlobCodec.RefuseAt:N0}):");
                foreach (var row in rows)
                    sb.AppendLine($"  {row.Key}: {row.Value.Stored:N0} bytes in the save ({row.Value.Raw:N0} raw, "
                                  + $"{100.0 * row.Value.Stored / Logic.Saving.SaveBlobCodec.EngineCeiling:0.0}% of the ceiling)");

                return sb.ToString();
            }
            catch (Exception ex) { return $"cr.save_sizes failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>
        ///   cr.compress [name]: forces a compression pass now, on the named character or (with no name) on
        ///   whichever one needs it most. Bypasses the daily pacing and the Mod Options toggle, never the SDK's
        ///   own guardrails (a first meeting, the recent events and every grave moment are always kept).
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("compress", "cr")]
        public static string Compress(List<string> args)
        {
            try
            {
                if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
                if (behavior.LlmServices is not {IsAvailable: true}) return "LLM services not available. Set your API key or endpoint first.";

                Hero hero = ResolveHero(args);

                if (hero == null)
                    return MemoryCompressionBehavior.Instance?.RunNow(true)
                        ?? "Memory-compression system is not active.";

                NpcProfile profile = behavior.Store.Get(hero.StringId);
                int count = profile?.Events?.Count ?? 0;

                if (count == 0) return $"{hero.Name} has no memories to compress.";

                _ = MemoryCompressionService.CompressAsync(behavior, hero.StringId, true);

                return $"Compressing {hero.Name}'s memory ({count} events) now. Watch the log for [MEMORY]; "
                     + "re-run cr.memory_load or cr.prompt_size to see the reduction.";
            }
            catch (Exception ex) { return $"cr.compress failed: {ex.GetType().Name}: {ex.Message}"; }
        }
    }
}
