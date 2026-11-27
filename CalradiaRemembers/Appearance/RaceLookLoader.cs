// Code written by Gabriel Mailhot, 26/11/2026.
// Reads races.json (Custom first): how each of the game's races is described (CatWithFourEyes, Discord 26/11/2026: non-human
// races of overhaul mods were read as "a man" or "a woman"; Gabriel, 26/11/2026: a file per race). Keyed by the game's race
// NAME (FaceGen.GetRaceNames), never its index, which moves with the modules loaded. RaceLookPolicy decides what an entry
// does; the shipped file holds examples only, so a stock game reads as before.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>Loads and keeps the race looks of races.json, and reads a hero's race name from the game.</summary>
    public static class RaceLookLoader
    {
        /// <summary>The file's name, in the Custom folder or ModuleData.</summary>
        public const string FileName = "races.json";

        /// <summary>The looks in force, by race name; empty before a load or when the file is missing or broken.</summary>
        public static IReadOnlyDictionary<string, RaceLook> Table { get; private set; } = new Dictionary<string, RaceLook>();

        /// <summary>Where the file was read from, for cr.test all and cr.lore_status.</summary>
        public static string LoadedFrom { get; private set; }

        /// <summary>
        ///   (Re)reads the file at <paramref name="path" /> (the caller resolves it Custom first, NarrativeLoader.ResolveCustomFirst,
        ///   so this namespace need not know Mapping, which knows it). Safe at each session launch and from cr.reload_prompts.
        /// </summary>
        public static void Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Table = new Dictionary<string, RaceLook>();
                    LoadedFrom = null;
                    ModLog.Info($"  {FileName} not found: every race reads as the face shows it. (looked in: {path})");

                    return;
                }

                var file = JsonConvert.DeserializeObject<FileShape>(File.ReadAllText(path)) ?? new FileShape();
                var table = new Dictionary<string, RaceLook>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, EntryShape> entry in file.Races ?? new Dictionary<string, EntryShape>())
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) continue;

                    table[entry.Key.Trim()] = new RaceLook {
                        Noun = entry.Value.Noun, FemaleNoun = entry.Value.Female, MaleNoun = entry.Value.Male, Line = entry.Value.Line,
                        Omit = entry.Value.Omit ?? new List<string>()
                    };
                }

                Table = table;
                LoadedFrom = path;
                ModLog.Info($"  Loaded {FileName}: {table.Count} race(s) described.");
            }
            catch (Exception ex)
            {
                Table = new Dictionary<string, RaceLook>();
                LoadedFrom = null;
                ModLog.ConfigError($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message}); every race reads as the face shows it until it is fixed.");
            }
        }

        /// <summary>The game's name for <paramref name="hero" />'s race ("human" in the stock game), or null when unreadable.</summary>
        public static string RaceNameOf(Hero hero)
        {
            try
            {
                int race = hero?.CharacterObject?.Race ?? -1;
                string[] names = FaceGen.GetRaceNames();

                return names != null && race >= 0 && race < names.Length ? names[race] : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The look written for <paramref name="hero" />'s race, or null.</summary>
        public static RaceLook LookOf(Hero hero) => RaceLookPolicy.Find(Table, RaceNameOf(hero));

        /// <summary>The noun <paramref name="hero" /> is called by ("elf woman"), or null for "woman"/"man".</summary>
        public static string KindNounOf(Hero hero) => hero == null ? null : RaceLookPolicy.KindNoun(LookOf(hero), hero.IsFemale);

        /// <summary>cr.test all seam: puts <paramref name="table" /> in force and returns the one it replaced.</summary>
        internal static IReadOnlyDictionary<string, RaceLook> SwapTableForTest(IReadOnlyDictionary<string, RaceLook> table)
        {
            IReadOnlyDictionary<string, RaceLook> before = Table;
            Table = table ?? new Dictionary<string, RaceLook>();

            return before;
        }

        private sealed class FileShape
        {
            [JsonProperty("races")] public Dictionary<string, EntryShape> Races { get; set; }
        }

        private sealed class EntryShape
        {
            [JsonProperty("noun")] public string Noun { get; set; }

            [JsonProperty("female")] public string Female { get; set; }

            [JsonProperty("male")] public string Male { get; set; }

            [JsonProperty("line")] public string Line { get; set; }

            [JsonProperty("omit")] public List<string> Omit { get; set; }
        }
    }
}
