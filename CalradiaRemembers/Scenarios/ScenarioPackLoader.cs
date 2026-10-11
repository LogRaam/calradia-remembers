// Code written by Gabriel Mailhot, 24/06/2026.
// Loads scripted-scenario packs: each file is a list of rules in the keyword DSL; a bad file is logged and skipped,
// never fatal. Mirrors JealousyCultureLoader.
// 11/10/2026 (Gabriel: scenarios supported by mods too): read from CR's ModuleData/scenarios, the player's
// Custom/scenarios and each enabled mod's ModuleData/cr_scenarios (ScenarioPackFolders), one rule per id
// (ScenarioRuleMerge), each rule remembering whose pack it came from.

#region

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>Reads and parses the scenario packs into <see cref="ScenarioRule" />s.</summary>
   public static class ScenarioPackLoader
   {
      /// <summary>CR's own packs folder.</summary>
      public static string ShippedFolder()
         => Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "ModuleData", "scenarios");

      /// <summary>Every pack: CR's own, the player's Custom ones, then each enabled mod's, in that order.</summary>
      public static ScenarioMergeResult LoadAll()
         => Load(ScenarioPackFolders.List(ShippedFolder(), Mapping.NarrativeLoader.CustomBaseFolder(), EnabledModules(), ScenarioPackFolders.OwnSource));

      /// <summary>
      ///   The packs of these folders, in order, one rule per id. A folder that does not exist is skipped silently; a
      ///   file that does not parse is logged and skipped. The seam cr.testall scenario_sources drives.
      /// </summary>
      public static ScenarioMergeResult Load(IReadOnlyList<ScenarioPackFolder> folders)
      {
         var read = new List<SourcedScenarioRule>();

         foreach (ScenarioPackFolder folder in folders ?? new List<ScenarioPackFolder>())
         {
            if (folder?.Folder == null || !Directory.Exists(folder.Folder)) continue;
            if (folder.Source != ScenarioPackFolders.OwnSource) ModLog.Info($"[SCENARIO] reading {folder.Source}'s scenario packs ({folder.Folder}).");

            foreach (string file in Directory.GetFiles(folder.Folder, "*.json"))
            {
               try
               {
                  ScenarioPackFile pack = JsonConvert.DeserializeObject<ScenarioPackFile>(File.ReadAllText(file));
                  if (pack?.Rules == null) continue;

                  foreach (ScenarioRuleDto dto in pack.Rules)
                  {
                     if (dto == null || string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Trigger)) continue;
                     read.Add(new SourcedScenarioRule
                     {
                        Source = folder.Source,
                        Rule = ScenarioRule.Parse(dto.Id, dto.Trigger, dto.Guards, dto.Consequences, dto.Message, dto.FireOnce, dto.Weight <= 0 ? 1 : dto.Weight, dto.Group)
                     });
                  }
               }
               catch (Exception ex)
               {
                  ModLog.Error($"  scenario pack '{folder.Source}: {Path.GetFileName(file)}' failed to parse: {ex.GetType().Name}: {ex.Message}");
               }
            }
         }

         ScenarioMergeResult result = ScenarioRuleMerge.Merge(read);
         foreach (string problem in result.Problems) ModLog.Info("[SCENARIO] " + problem);
         ModLog.Info($"  Loaded {result.Rules.Count} scripted scenario rule(s).");

         return result;
      }

      // The other enabled mods with their roots, in launcher order (the same enabled list cr_patch and tales read).
      private static IEnumerable<(string Id, string Root)> EnabledModules()
      {
         var modules = new List<(string, string)>();
         try
         {
            foreach (string id in Storage.NarrativePatchScanner.LoadedModuleIds())
            {
               string root;
               try { root = TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath(id); }
               catch { continue; }
               modules.Add((id, root));
            }
         }
         catch (Exception ex)
         {
            ModLog.Fault("listing other mods' scenario folders", ex);
         }

         return modules;
      }

      /// <summary>On-disk shape: a doc <c>_comment</c> (ignored) plus the list of rules.</summary>
      private sealed class ScenarioPackFile
      {
         [JsonProperty("rules")]
         public List<ScenarioRuleDto> Rules { get; set; }
      }

      private sealed class ScenarioRuleDto
      {
         [JsonProperty("id")]           public string Id { get; set; }
         [JsonProperty("trigger")]      public string Trigger { get; set; }
         [JsonProperty("guards")]       public List<string> Guards { get; set; }
         [JsonProperty("consequences")] public List<string> Consequences { get; set; }
         [JsonProperty("message")]      public string Message { get; set; }
         [JsonProperty("fireOnce")]     public bool FireOnce { get; set; }
         [JsonProperty("weight")]       public int Weight { get; set; } = 1;
         [JsonProperty("group")]        public string Group { get; set; }
      }
   }
}
