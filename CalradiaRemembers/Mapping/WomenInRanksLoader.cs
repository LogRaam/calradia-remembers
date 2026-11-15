// Code written by Gabriel Mailhot, 14/11/2026.
// Reads women_in_ranks.json (Custom first): the share of women per culture and per realm among the soldiers in battle
// (Gabriel, 14/11/2026; see Logic.Troops.WomenInRanksPolicy). A file of its own rather than a field in
// cultural_traditions.json: that one is a plain culture -> prose map shared in format with other mods, and players keep
// their own copies of it, which a new field would break.

#region

using System;
using System.Collections.Generic;
using System.IO;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Troops;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Loads and keeps the women-in-the-ranks table.</summary>
   public static class WomenInRanksLoader
   {
      /// <summary>The file's name, in the Custom folder or ModuleData.</summary>
      public const string FileName = "women_in_ranks.json";

      /// <summary>The table in force; empty (no women added anywhere) before a load or when the file is missing or broken.</summary>
      public static WomenInRanksTable Table { get; private set; } = new WomenInRanksTable();

      /// <summary>Where the table was read from, for cr.test all.</summary>
      public static string LoadedFrom { get; private set; }

      /// <summary>(Re)reads the file. Safe to call at each session launch and from cr.reload_prompts.</summary>
      public static void Load()
      {
         string path = NarrativeLoader.ResolveCustomFirst(FileName);
         try
         {
            if (!File.Exists(path))
            {
               Table = new WomenInRanksTable();
               LoadedFrom = null;
               ModLog.Info($"  {FileName} not found: no women added to the ranks. (looked in: {path})");

               return;
            }

            var file = JsonConvert.DeserializeObject<FileShape>(File.ReadAllText(path)) ?? new FileShape();
            Table = new WomenInRanksTable {
               Cultures = new Dictionary<string, int>(file.Cultures ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase),
               Realms = new Dictionary<string, int>(file.Realms ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase)
            };
            LoadedFrom = path;
            ModLog.Info($"  Loaded {FileName}: {Table.Cultures.Count} culture(s), {Table.Realms.Count} realm(s).");
         }
         catch (Exception ex)
         {
            Table = new WomenInRanksTable();
            LoadedFrom = null;
            ModLog.ConfigError($"{FileName} could not be read ({ex.GetType().Name}: {ex.Message}); no women added to the ranks until it is fixed.");
         }
      }

      private sealed class FileShape
      {
         [JsonProperty("cultures")] public Dictionary<string, int> Cultures { get; set; }

         [JsonProperty("realms")] public Dictionary<string, int> Realms { get; set; }
      }
   }
}
