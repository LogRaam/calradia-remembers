// Code written by Gabriel Mailhot, 29/06/2026.

#region

using System.Collections.Generic;
using System.IO;
using System.Text;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>
   ///   Loads narrative text files from the mod's ModuleData folder at session launch.
   ///   All three files are optional — missing files produce empty strings and the
   ///   PromptBuilder falls back to its hardcoded defaults.
   ///   Reload on every session launch so edits take effect without restarting the game.
   /// </summary>
   public static class NarrativeLoader
   {
      private static readonly Dictionary<string, string> _stationGuidelines = new();
      public static string BehaviorGuidelines { get; private set; } = "";
      public static string PlayerDescription { get; private set; } = "";
      public static string PostHistoryInstructions { get; private set; } = "";
      public static string WorldDescription { get; private set; } = "";

      public static void Load()
      {
         string moduleData = Path.Combine(
            BasePath.Name, "Modules", "CalradiaRemembers", "ModuleData");

         WorldDescription = ReadFile(moduleData, "world.txt", "world description");
         PlayerDescription = ReadFile(moduleData, "player_description.txt", "player description");
         BehaviorGuidelines = ReadFile(moduleData, "behavior_guidelines.txt", "behavior guidelines");
         PostHistoryInstructions = ReadFile(moduleData, "post_history_instructions.txt", "post-history instructions");

         _stationGuidelines.Clear();
         foreach (string station in new[] {"gangleader", "notable", "wanderer"})
            _stationGuidelines[station] = ReadFile(moduleData, $"behavior_guidelines_{station}.txt", $"{station} guidelines");
      }

      /// <summary>
      ///   The station-specific behavior guidelines for a station key ("gangleader", "notable",
      ///   "wanderer"), or "" when that station file is absent — the caller's fallback chain then
      ///   applies (see StationGuidelinesPolicy).
      /// </summary>
      public static string StationGuidelines(string stationKey)
         => stationKey != null && _stationGuidelines.TryGetValue(stationKey, out string text)
            ? text
            : "";

      #region private

      private static string ReadFile(string folder, string filename, string label)
      {
         string path = Path.Combine(folder, filename);
         if (!File.Exists(path))
         {
            ModLog.Info($"  [{label}] not found — using built-in default. ({path})");

            return "";
         }

         string content = File.ReadAllText(path, Encoding.UTF8).Trim();
         ModLog.Info($"  [{label}] loaded ({content.Length} chars).");

         return content;
      }

      #endregion
   }
}