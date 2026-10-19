// Code written by Gabriel Mailhot, 29/06/2026.

#region

using CalradiaRemembers.Foundation;
using CalradiaRemembers.Storage;
using CalradiaRemembers.Logic.Narrative;
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
   ///   A file under ModuleData/Custom of the same name is read instead when present, so a
   ///   player's own edits survive a mod update that re-extracts ModuleData.
   /// </summary>
   public static class NarrativeLoader
   {
      private static readonly Dictionary<string, string> _stationGuidelines = new();
      public static string BehaviorGuidelines { get; private set; } = "";
      public static string PlayerDescription { get; private set; } = "";
      public static string PostHistoryInstructions { get; private set; } = "";

      /// <summary>heard_now.txt: CR ships none; another mod's live knowledge, through cr_patch (philip0105, 10/10/2026).</summary>
      public static string HeardNowInstructions { get; private set; } = "";
      public static string WorldDescription { get; private set; } = "";

      /// <summary>
      ///   council_seat.txt: a template expanded once per seated council member with that member's own variables
      ///   (CouncilSeatNoteResolver). CR ships none; it exists for other mods, through cr_patch (tashmetu, 26/09/2026).
      /// </summary>
      public static string CouncilSeatTemplate { get; private set; } = "";

      public static void Load()
      {
         WorldDescription = ReadFile("world.txt", "world description");
         PlayerDescription = ReadFile("player_description.txt", "player description");
         BehaviorGuidelines = ReadFile("behavior_guidelines.txt", "behavior guidelines");
         PostHistoryInstructions = ReadFile("post_history_instructions.txt", "post-history instructions");
         CouncilSeatTemplate = ReadFile("council_seat.txt", "council seat");
         HeardNowInstructions = ReadFile("heard_now.txt", "heard now");

         _stationGuidelines.Clear();
         foreach (string station in new[] {"gangleader", "notable", "wanderer", "companion"})
            _stationGuidelines[station] = ReadFile($"behavior_guidelines_{station}.txt", $"{station} guidelines");

         // A notable's own trade, read before the shared notable file (Windmolen, 19/10/2026). None ships.
         foreach (string trade in CalradiaRemembers.Logic.StationGuidelinesPolicy.TradeKeys)
            _stationGuidelines[trade] = ReadFile($"behavior_guidelines_{trade}.txt", $"{trade} guidelines");
      }

      /// <summary>
      ///   The base folder for player overrides. It is the MCM "Custom Folder Path" when the player has set it to
      ///   an existing folder, otherwise the shipped <c>ModuleData/Custom</c>. A player who points this at a folder
      ///   OUTSIDE the module keeps their prompts/styles safe from a mod update that replaces the module folder
      ///   (player report v1.33.5: a clean reinstall wiped Custom); they just re-enter the path after an update, and
      ///   it also lets them keep several prompt sets and switch between them. Shared by <see cref="ResolveCustomFirst" />
      ///   and StyleCatalog so ALL overrides honour the one path.
      /// </summary>
      public static string CustomBaseFolder()
      {
         string configured = CalradiaRemembers.Foundation.ModSettings.CustomFolderPath;
         if (!string.IsNullOrWhiteSpace(configured))
            try { if (Directory.Exists(configured)) return configured; }
            catch { /* a malformed path silently falls back to the shipped default below */ }

         return Path.Combine(ModuleDataFolder(), "Custom");
      }

      /// <summary>
      ///   Resolves <paramref name="filename" /> Custom-first: the absolute path under
      ///   <see cref="CustomBaseFolder" /> when that file exists, otherwise the shipped ModuleData path (returned
      ///   whether or not it exists, so callers keep handling a missing file themselves). Shared by every ModuleData
      ///   reader (narrative text, cultural_traditions.json, jealousy_cultures.json) so a player's edits survive a
      ///   mod update.
      /// </summary>
      public static string ResolveCustomFirst(string filename)
      {
         string customPath = Path.Combine(CustomBaseFolder(), filename);

         return File.Exists(customPath) ? customPath : Path.Combine(ModuleDataFolder(), filename);
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

      /// <summary>
      ///   Test seam for cr.testall: sets one station or trade text in memory and returns what was there, so the
      ///   test can put it back. Never called by the game.
      /// </summary>
      internal static string ReplaceStationGuidelinesForTest(string key, string text)
      {
         string before = StationGuidelines(key);
         _stationGuidelines[key] = text ?? "";

         return before;
      }

      #region private

      // internal so sibling loaders (e.g. SettingProfileLoader) can report whether they resolved a Custom override.
      internal static string ModuleDataFolder()
         => Path.Combine(CalradiaRemembers.Paths.ModulePaths.Root, "ModuleData");

      private static string ReadFile(string filename, string label)
      {
         string path = ResolveCustomFirst(filename);
         bool useCustom = path != Path.Combine(ModuleDataFolder(), filename);
         // A file CR does not ship (council_seat.txt, heard_now.txt) is an empty base that other mods' patches still
         // fill: returning early here meant a module's cr_patch for such a file was never read (found 10/10/2026).
         string content = "";
         if (!File.Exists(path)) ModLog.Info($"  [{label}] not found, empty base. ({path})");
         else
         {
            content = File.ReadAllText(path, Encoding.UTF8).Trim();
            ModLog.Info($"  [{label}]{(useCustom ? " (custom)" : "")} loaded ({content.Length} chars).");
         }

         // OTHER MODULES GET THEIR SAY, AFTER OURS AND AFTER THE PLAYER'S. The Custom folder above is the
         // PLAYER's override and stays the last word on what the base text is; patches are laid over whatever
         // that resolved to, in launcher load order. A player with no bridge mod installed gets back the exact
         // bytes read above, which NarrativePatchComposer guarantees rather than merely tends to do.
         IReadOnlyList<NarrativePatch> patches = NarrativePatchScanner.PatchesFor(filename);

         if (patches.Count == 0) return content;

         string composed = NarrativePatchComposer.Compose(content, patches);
         ModLog.Info($"  [{label}] {patches.Count} module patch(es) applied ({content.Length} -> {composed.Length} chars).");

         return composed;
      }

      #endregion
   }
}