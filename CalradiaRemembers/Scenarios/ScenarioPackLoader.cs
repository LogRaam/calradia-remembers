// Code written by Gabriel Mailhot, 24/06/2026.
// Loads scripted-scenario packs from ModuleData/scenarios/*.json — the modder-facing surface. Each file is a
// list of rules in the keyword DSL; a bad file is logged and skipped, never fatal. Mirrors JealousyCultureLoader.

#region

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;
using CalradiaRemembers.Logic;

#endregion

namespace CalradiaRemembers.Scenarios
{
   /// <summary>Reads and parses the scenario packs from ModuleData/scenarios into <see cref="ScenarioRule" />s.</summary>
   public static class ScenarioPackLoader
   {
      public static List<ScenarioRule> LoadAll()
      {
         var rules = new List<ScenarioRule>();
         string dir = Path.Combine(
            BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData", "scenarios");

         if (!Directory.Exists(dir))
         {
            ModLog.Info($"  no scenarios folder — no scripted scenarios loaded. (looked in: {dir})");
            return rules;
         }

         foreach (string file in Directory.GetFiles(dir, "*.json"))
         {
            try
            {
               string json = File.ReadAllText(file);
               ScenarioPackFile pack = JsonConvert.DeserializeObject<ScenarioPackFile>(json);
               if (pack?.Rules == null) continue;

               foreach (ScenarioRuleDto dto in pack.Rules)
               {
                  if (dto == null || string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Trigger)) continue;
                  rules.Add(ScenarioRule.Parse(
                     dto.Id, dto.Trigger, dto.Guards, dto.Consequences, dto.Message, dto.FireOnce, dto.Weight <= 0 ? 1 : dto.Weight));
               }
            }
            catch (Exception ex)
            {
               ModLog.Error($"  scenario pack '{Path.GetFileName(file)}' failed to parse: {ex.GetType().Name}: {ex.Message}");
            }
         }

         ModLog.Info($"  Loaded {rules.Count} scripted scenario rule(s) from ModuleData/scenarios.");
         return rules;
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
      }
   }
}
