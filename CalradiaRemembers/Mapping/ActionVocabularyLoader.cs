// Code written by Gabriel Mailhot, 30/05/2026. Sprint 10.

#region

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NpcMemoryService.Core.Prompts;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Mapping
{
    /// <summary>
    ///   Loads and caches the action vocabulary from <c>action_vocabulary.json</c>
    ///   in the mod's ModuleData folder. The vocabulary defines which [ACTION] types
    ///   the LLM may emit and what parameters each accepts.
    ///   Falls back to an empty list if the file is absent — actions are disabled.
    /// </summary>
    public static class ActionVocabularyLoader
    {
        private static IReadOnlyList<GameActionDefinition> _vocabulary = new List<GameActionDefinition>();

        public static IReadOnlyList<GameActionDefinition> Vocabulary => _vocabulary;

        /// <summary>
        ///   Loads (or reloads) the JSON file. Safe to call on every session launch.
        /// </summary>
        public static void Load()
        {
            string path = Path.Combine(
                BasePath.Name, "Modules", "CalradiaRemembers",
                "ModuleData", "action_vocabulary.json");

            if (!File.Exists(path))
            {
                ModLog.Info("  action_vocabulary.json not found — ACTION vocabulary disabled.");
                _vocabulary = new List<GameActionDefinition>();
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                List<ActionDefinitionDto> dtos =
                    JsonConvert.DeserializeObject<List<ActionDefinitionDto>>(json)
                    ?? new List<ActionDefinitionDto>();

                var list = new List<GameActionDefinition>();
                foreach (ActionDefinitionDto dto in dtos)
                {
                    if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Description))
                        continue;

                    list.Add(new GameActionDefinition {
                        Type        = dto.Type.Trim(),
                        Description = dto.Description.Trim(),
                        Parameters  = dto.Parameters ?? new List<string>()
                    });
                }

                _vocabulary = list;
                ModLog.Info($"  Loaded {list.Count} action definition(s) from action_vocabulary.json.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to load action_vocabulary.json: {ex.Message}");
                _vocabulary = new List<GameActionDefinition>();
            }
        }

        // ── Private DTO ───────────────────────────────────────────────────────

        private sealed class ActionDefinitionDto
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("parameters")]
            public List<string> Parameters { get; set; }
        }
    }
}
