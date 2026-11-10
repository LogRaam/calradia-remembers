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
        ///   cr.save_sizes: what each CR store weighed at the last save of this session, raw, compressed and in pieces (tashmetu,
        ///   09/11/2026: a byte[] store past 16,777,215 bytes is saved without error and never loads; since 2.8.8 stores are
        ///   saved in Base64 pieces), and where each was read from at the last load. Mutates nothing.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("save_sizes", "cr")]
        public static string SaveSizes(List<string> args)
        {
            try
            {
                var rows = Foundation.SaveBlob.LastSaved;
                var loads = Foundation.SaveBlob.LastLoaded;
                string rescue = $"Save rescue: {(Saving.SaveRescuePatch.Attached ? "attached" : "NOT attached (see the log)")}.";
                var sb = new System.Text.StringBuilder();
                if (loads.Count > 0)
                {
                    sb.AppendLine("Read at the last load from:");
                    foreach (var row in loads)
                        sb.AppendLine($"  {row.Key}: {row.Value}");
                }

                if (rows.Count == 0)
                    return sb + "No CR store has been saved in this session yet: save the game, then run cr.save_sizes again. " + rescue;

                sb.AppendLine($"CR stores at the last save, in Base64 pieces of {Logic.Saving.SaveBlobCodec.PieceLength:N0} characters "
                              + $"(CR refuses to write one past {Logic.Saving.SaveBlobCodec.RefuseAt:N0} bytes compressed):");
                foreach (var row in rows)
                    sb.AppendLine($"  {row.Key}: {row.Value.Raw:N0} bytes raw, {row.Value.Stored:N0} compressed, {row.Value.Pieces:N0} piece(s)");

                sb.AppendLine(rescue);

                return sb.ToString();
            }
            catch (Exception ex) { return $"cr.save_sizes failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>
        ///   cr.inflate_history &lt;megabytes&gt; confirm: adds that much invented history to the campaign in play, so a large save
        ///   can be measured without a long campaign (Gabriel, 10/11/2026: "peux-tu faire une commande console qui ajoute
        ///   beaucoup d'historique a la partie en cours pour gonfler la sauvegarde?"). About two thirds as memories, written
        ///   by a word chain learned from the campaign's own memories and the shipped folklore (MarkovText, seeded so the
        ///   same campaign inflates the same way), spread over the existing profiles; one third as relation-ledger pairs
        ///   between living heroes, each with a start the prune keeps. For a COPY of a campaign: it cannot be undone.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("inflate_history", "cr")]
        public static string InflateHistory(List<string> args)
        {
            try
            {
                if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
                if (args == null || args.Count < 2 || !int.TryParse(args[0], out int megabytes) || megabytes <= 0
                    || !string.Equals(args[1], "confirm", StringComparison.OrdinalIgnoreCase))
                    return "Usage: cr.inflate_history <megabytes> confirm. Adds that much invented history (memories and relation pairs) "
                           + "to THIS campaign, for good: run it on a copy, and turn MCM Memory Compression off there first, or the daily "
                           + "compaction will spend model calls summarising the invented memories.";

                Mapping.RelationLedgerBehavior ledgerBehavior = Mapping.RelationLedgerBehavior.Instance;
                List<string> ids = behavior.Store.ListIds().ToList();
                if (ids.Count == 0) return "No character has a memory profile yet: talk to a few first, then inflate.";

                // What the chain learns from: every memory the campaign holds, then the folklore's lines as a floor.
                var samples = new List<string>();
                foreach (string id in ids)
                {
                    NpcProfile p = behavior.Store.Get(id);
                    if (p?.Events != null) samples.AddRange(p.Events.Where(e => !string.IsNullOrWhiteSpace(e?.summary)).Select(e => e.summary));
                }

                int fromCampaign = samples.Count;
                string folklore = System.IO.Path.Combine(TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath("CalradiaRemembers"), "ModuleData", "folklore");
                if (System.IO.Directory.Exists(folklore))
                    foreach (string file in System.IO.Directory.GetFiles(folklore, "*.txt"))
                        samples.AddRange(System.IO.File.ReadAllLines(file)
                                                .Where(l => l.StartsWith("gist:", StringComparison.Ordinal) || l.StartsWith("beat:", StringComparison.Ordinal))
                                                .Select(l => l.Substring(5).Trim()));

                var chain = new Logic.Saving.MarkovText(samples);
                if (chain.IsEmpty) return "Nothing to learn the invented memories from (no memory in this campaign, no folklore files).";

                var random = new Random(ids.Count * 7919 + fromCampaign);
                int today = behavior.GameStateBridge.GetCurrentGameDay();
                NotableEventType[] kinds = {NotableEventType.Other, NotableEventType.Agreement, NotableEventType.Collaboration, NotableEventType.Conflict, NotableEventType.Confrontation, NotableEventType.Flirt};

                // Memories: about two thirds of the target, a sentence of 12 to 45 words at a time, round the profiles.
                long memoryTarget = megabytes * 1024L * 1024L * 2 / 3, written = 0;
                var touched = new HashSet<string>();
                for (var i = 0; written < memoryTarget; i++)
                {
                    string id = ids[i % ids.Count];
                    NpcProfile p = behavior.Store.Get(id);
                    if (p?.Events == null) continue;

                    string summary = chain.Sentence(random, 12 + random.Next(34));
                    if (summary.Length == 0) continue;

                    p.Events.Add(new NotableEvent(Math.Max(0, today - random.Next(Math.Max(1, today))), kinds[random.Next(kinds.Length)], summary));
                    written += summary.Length + 60; // the JSON around each memory
                    touched.Add(id);
                }

                foreach (string id in touched) behavior.Store.Set(behavior.Store.Get(id));

                // Relation pairs: the other third, between living heroes, each start away from today's value so the prune keeps it.
                var added = 0;
                if (ledgerBehavior != null)
                {
                    List<Hero> heroes = Hero.AllAliveHeroes.Where(h => h != null && h.StringId != null).ToList();
                    long ledgerTarget = megabytes * 1024L * 1024L / 3, ledgerWritten = 0;
                    for (var tries = 0; ledgerWritten < ledgerTarget && heroes.Count > 1 && tries < 50_000_000; tries++)
                    {
                        Hero a = heroes[random.Next(heroes.Count)], b = heroes[random.Next(heroes.Count)];
                        if (a == b) continue;

                        int now = CharacterRelationManager.GetHeroRelation(a, b);
                        int shift = (random.Next(2) == 0 ? -1 : 1) * (5 + random.Next(40));
                        int start = Math.Max(-100, Math.Min(100, now + shift));
                        if (start == now) start = now > 0 ? now - 5 : now + 5;
                        if (!ledgerBehavior.Ledger.Saw(a.StringId, b.StringId, start)) continue;

                        added++;
                        ledgerWritten += a.StringId.Length + b.StringId.Length + 8;
                    }
                }

                ModLog.Info($"[SAVE] cr.inflate_history: {megabytes} MB asked; ~{written:N0} bytes of memories on {touched.Count} profile(s), "
                            + $"{added:N0} relation pair(s). Learned from {fromCampaign:N0} campaign memories and {samples.Count - fromCampaign:N0} folklore lines.");

                return $"Inflated: about {written / 1024 / 1024} MB of invented memories over {touched.Count} profile(s) and {added:N0} relation pairs. "
                       + "Save now (to a new slot), then cr.save_sizes; cr.dump_stores before saving writes the stores for Tools/CodecBench. "
                       + "Invented text is not real text: codec numbers on it are indicative only.";
            }
            catch (Exception ex) { return $"cr.inflate_history failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>
        ///   cr.dump_stores: arms a dump of each compressed CR store's JSON, exactly as the next save writes it, into the
        ///   Logs folder's cr_stores, so codecs can be compared outside the game (Tools/CodecBench). Armed for ten minutes;
        ///   save the game to write them. Mutates nothing in the campaign.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("dump_stores", "cr")]
        public static string DumpStores(List<string> args)
        {
            try
            {
                Foundation.SaveBlob.ArmDump(10);

                return $"Armed for 10 minutes: save the game now, and each CR store's JSON is written to {Foundation.SaveBlob.DumpFolder}. "
                       + "Those files hold the campaign's memories and relations: share them only where you mean to.";
            }
            catch (Exception ex) { return $"cr.dump_stores failed: {ex.GetType().Name}: {ex.Message}"; }
        }

        /// <summary>
        ///   cr.save_rescue_probe [arm|status]: proves the save rescue in game. "arm" makes the next save write one plain byte[]
        ///   just past the engine's ceiling, the broken save tashmetu reported (09/11/2026); loading that save must then open,
        ///   with the probe empty. ON A COPY of a campaign only: that save grows by about 320 MB.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("save_rescue_probe", "cr")]
        public static string SaveRescueProbe(List<string> args)
        {
            string verb = args != null && args.Count > 0 ? args[0].Trim().ToLowerInvariant() : "status";
            if (verb == "arm")
            {
                Saving.SaveRescueProbe.Armed = true;

                return "Armed: the next save writes a block just past the engine's ceiling (about 320 MB more). Use a COPY of a campaign: "
                       + "save under a new name, then load that save. It should open, with a yellow repair notice, and cr.save_rescue_probe status should say 0.";
            }

            if (verb == "disarm")
            {
                Saving.SaveRescueProbe.Armed = false;

                return "Disarmed: the next save writes no probe.";
            }

            int? last = Saving.SaveRescueProbe.LastLoadedLength;
            return $"Save rescue {(Saving.SaveRescuePatch.Attached ? "attached" : "NOT attached")}. Probe armed: {Saving.SaveRescueProbe.Armed}. "
                   + (last == null ? "The last load held no probe."
                      : last == 0 ? "The last load held the probe and the rescue emptied it: the rescue works."
                      : $"The last load held a probe of {last:N0} bytes (not emptied).");
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
